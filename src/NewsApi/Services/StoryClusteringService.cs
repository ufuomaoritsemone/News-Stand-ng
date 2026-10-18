using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Models;
using NewsApi.Services.Embeddings;

namespace NewsApi.Services;

public class StoryClusteringService : IStoryClusteringService
{
    private readonly NewsDbContext _db;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ISlugService _slugService;
    private readonly IConfiguration _config;
    private readonly ILogger<StoryClusteringService> _logger;
    private readonly IMemoryCache? _cache;

    public StoryClusteringService(
        NewsDbContext db,
        IEmbeddingProvider embeddingProvider,
        ISlugService slugService,
        IConfiguration config,
        ILogger<StoryClusteringService> logger,
        IMemoryCache? cache = null)
    {
        _db = db;
        _embeddingProvider = embeddingProvider;
        _slugService = slugService;
        _config = config;
        _logger = logger;
        _cache = cache;
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "after", "again", "against", "amid", "among", "around", "because", "before",
        "between", "breaking", "could", "despite", "during", "every", "first", "from", "government",
        "have", "into", "lagos", "more", "most", "news", "nigeria", "nigerian", "nigerians",
        "other", "over", "report", "reports", "says", "said", "should", "since", "state", "states",
        "their", "there", "these", "they", "this", "through", "today", "under", "until", "update",
        "what", "when", "where", "which", "while", "with", "within", "would", "year", "years"
    };

    public async Task<int> RunClusteringAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("StoryClusteringService: Starting clustering cycle for last 72 hours...");
        var cutoff = DateTime.UtcNow.AddHours(-72);

        // 1. Generate embeddings for any articles in the 72h window missing an embedding
        await EnsureEmbeddingsAsync(cutoff, cancellationToken);

        // 2. Load all articles and embeddings in the 72h window via direct join
        var articlePairs = await (
            from art in _db.Articles
            join emb in _db.ArticleEmbeddings on art.Id equals emb.ArticleId
            where art.PublishedAt >= cutoff
            select new { Article = art, Embedding = emb }
        ).ToListAsync(cancellationToken);

        // Fallback to a 14-day window if recent 72h articles are sparse
        if (articlePairs.Count < 4)
        {
            cutoff = DateTime.UtcNow.AddDays(-14);
            await EnsureEmbeddingsAsync(cutoff, cancellationToken);
            articlePairs = await (
                from art in _db.Articles
                join emb in _db.ArticleEmbeddings on art.Id equals emb.ArticleId
                where art.PublishedAt >= cutoff
                select new { Article = art, Embedding = emb }
            ).ToListAsync(cancellationToken);
        }

        if (articlePairs.Count < 2)
        {
            _logger.LogInformation("StoryClusteringService: Insufficient articles with embeddings ({Count}) to form clusters.", articlePairs.Count);
            return 0;
        }

        var defaultThreshold = _embeddingProvider is LocalFallbackEmbeddingProvider
            ? 0.48f
            : 0.58f;
        var threshold = _config.GetValue<float>("Clustering:SimilarityThreshold", defaultThreshold);
        _logger.LogInformation("Clustering {Count} articles with cosine threshold {Threshold}", articlePairs.Count, threshold);

        // 3. Cluster articles using Greedy Leader clustering with title-token overlap boost
        var candidates = new List<ClusterCandidate>();

        foreach (var item in articlePairs)
        {
            var vector = item.Embedding.Vector;
            if (vector.Length == 0 || item.Article == null) continue;

            ClusterCandidate? bestCandidate = null;
            float bestSim = 0f;

            foreach (var cand in candidates)
            {
                var cosineSim = VectorMath.CosineSimilarity(vector, cand.Centroid);
                var keywordBoost = ComputeTitleKeywordBoost(item.Article.Title, cand.Items[0].Article.Title);
                var sim = Math.Min(1.0f, cosineSim + keywordBoost);
                if (sim >= threshold && sim > bestSim)
                {
                    bestSim = sim;
                    bestCandidate = cand;
                }
            }

            if (bestCandidate != null)
            {
                bestCandidate.Add(item.Article, vector, bestSim);
            }
            else
            {
                var newCand = new ClusterCandidate(item.Article, vector);
                candidates.Add(newCand);
            }
        }

        // 4. Filter candidates: Must have >= 2 articles AND >= 2 distinct sources
        var validCandidates = candidates
            .Where(c => c.Items.Count >= 2 && c.Sources.Count >= 2)
            .OrderByDescending(c => c.Items.Count)
            .ToList();

        _logger.LogInformation("Identified {ClusterCount} multi-source story clusters from {TotalCandidates} candidates",
            validCandidates.Count, candidates.Count);

        // 5. Persist clusters to database
        var existingClusters = await _db.StoryClusters
            .Include(sc => sc.ClusterArticles)
            .Where(sc => sc.UpdatedAt >= cutoff)
            .ToListAsync(cancellationToken);

        int updatedClustersCount = 0;

        foreach (var cand in validCandidates)
        {
            var articleIds = cand.Items.Select(i => i.Article.Id).ToHashSet();

            // Check if any existing cluster already contains some of these articles
            var matchedCluster = existingClusters.FirstOrDefault(sc =>
                sc.ClusterArticles.Any(ca => articleIds.Contains(ca.ArticleId)));

            if (matchedCluster == null)
            {
                // Create new cluster
                var repArticle = cand.Items.OrderBy(i => i.Article.PublishedAt ?? DateTime.MaxValue).First().Article;
                var title = repArticle.Title;
                var baseSlug = _slugService.GenerateSlug(title);
                var slug = baseSlug;
                int suffix = 1;
                while (await _db.StoryClusters.AnyAsync(s => s.Slug == slug, cancellationToken) ||
                       existingClusters.Any(s => s.Slug == slug))
                {
                    slug = $"{baseSlug}-{suffix++}";
                }

                matchedCluster = new StoryCluster
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Slug = slug,
                    Title = title,
                    Summary = cand.Items.Select(i => i.Article.Summary).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                              ?? cand.Items.Select(i => i.Article.Excerpt).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
                    ImageUrl = CategoryImageMap.Resolve(
                        cand.Items.Select(i => i.Article.ImageUrl).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u)),
                        cand.Items.Select(i => i.Article.Category).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))),
                    CreatedAt = cand.Items.Min(i => i.Article.PublishedAt ?? DateTime.UtcNow),
                    UpdatedAt = cand.Items.Max(i => i.Article.PublishedAt ?? DateTime.UtcNow),
                    ArticleCount = cand.Items.Count,
                    SourceCount = cand.Sources.Count
                };

                _db.StoryClusters.Add(matchedCluster);
                existingClusters.Add(matchedCluster);
            }
            else
            {
                // Update existing cluster
                matchedCluster.ArticleCount = cand.Items.Count;
                matchedCluster.SourceCount = cand.Sources.Count;
                matchedCluster.UpdatedAt = cand.Items.Max(i => i.Article.PublishedAt ?? DateTime.UtcNow);
                if (string.IsNullOrEmpty(matchedCluster.ImageUrl))
                {
                    matchedCluster.ImageUrl = CategoryImageMap.Resolve(
                        cand.Items.Select(i => i.Article.ImageUrl).FirstOrDefault(u => !string.IsNullOrWhiteSpace(u)),
                        cand.Items.Select(i => i.Article.Category).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)));
                }
            }

            // Synchronize join records
            var currentLinks = matchedCluster.ClusterArticles.ToDictionary(ca => ca.ArticleId);
            foreach (var (article, vector, sim) in cand.Items)
            {
                if (currentLinks.TryGetValue(article.Id, out var existingLink))
                {
                    existingLink.SimilarityScore = sim > 0 ? sim : 1.0f;
                }
                else
                {
                    var newLink = new StoryClusterArticle
                    {
                        ClusterId = matchedCluster.Id,
                        ArticleId = article.Id,
                        SimilarityScore = sim > 0 ? sim : 1.0f,
                        AddedAt = DateTime.UtcNow
                    };
                    matchedCluster.ClusterArticles.Add(newLink);
                    _db.StoryClusterArticles.Add(newLink);
                }
            }

            updatedClustersCount++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("StoryClusteringService: Successfully saved {Count} story clusters", updatedClustersCount);
        return updatedClustersCount;
    }

    public async Task<List<Article>> FindRelatedArticlesAsync(string articleId, int limit = 6, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return [];

        var effectiveLimit = Math.Clamp(limit, 1, 20);
        var cacheKey = $"related_articles_v1_{articleId}_{effectiveLimit}";

        if (_cache != null && _cache.TryGetValue(cacheKey, out List<Article>? cached) && cached is { Count: > 0 })
        {
            return cached;
        }

        var result = await ComputeRelatedArticlesCoreAsync(articleId, effectiveLimit, cancellationToken);

        if (_cache != null && result.Count > 0)
        {
            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(10),
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            });
        }

        return result;
    }

    private async Task<List<Article>> ComputeRelatedArticlesCoreAsync(string articleId, int limit, CancellationToken cancellationToken)
    {
        var targetBlob = await _db.ArticleEmbeddings
            .AsNoTracking()
            .Where(ae => ae.ArticleId == articleId)
            .Select(ae => ae.VectorBlob)
            .FirstOrDefaultAsync(cancellationToken);

        if (targetBlob is { Length: > 0 })
        {
            var targetVector = new float[targetBlob.Length / sizeof(float)];
            Buffer.BlockCopy(targetBlob, 0, targetVector, 0, targetBlob.Length);

            var windowCutoff = DateTime.UtcNow.AddDays(-14);

            // Project ONLY ArticleId + VectorBlob (without full Article Content bodies) bounded to top 500 recent embeddings
            var candidateVectors = await _db.ArticleEmbeddings
                .AsNoTracking()
                .Where(ae => ae.ArticleId != articleId && ae.Article != null && ae.Article.PublishedAt >= windowCutoff)
                .OrderByDescending(ae => ae.Article!.PublishedAt)
                .Take(500)
                .Select(ae => new { ae.ArticleId, ae.VectorBlob })
                .ToListAsync(cancellationToken);

            var scoredIds = new List<(string ArticleId, float Score)>();
            var scratchVector = new float[targetVector.Length];

            foreach (var cand in candidateVectors)
            {
                if (cand.VectorBlob is null || cand.VectorBlob.Length != targetBlob.Length)
                    continue;

                Buffer.BlockCopy(cand.VectorBlob, 0, scratchVector, 0, cand.VectorBlob.Length);
                var sim = VectorMath.CosineSimilarity(targetVector, scratchVector);
                if (sim >= 0.45f)
                {
                    scoredIds.Add((cand.ArticleId, sim));
                }
            }

            var topMatches = scoredIds
                .OrderByDescending(s => s.Score)
                .Take(limit)
                .ToList();

            if (topMatches.Count >= Math.Min(limit, 3))
            {
                var topIds = topMatches.Select(m => m.ArticleId).ToList();
                var matchedArticles = await _db.Articles
                    .AsNoTracking()
                    .Where(a => topIds.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id, cancellationToken);

                var orderedArticles = topIds
                    .Where(matchedArticles.ContainsKey)
                    .Select(id => matchedArticles[id])
                    .ToList();

                if (orderedArticles.Count >= Math.Min(limit, 3))
                {
                    return orderedArticles;
                }
            }
        }

        // Fallback: Category + Topic matching
        var targetArticle = await _db.Articles
            .AsNoTracking()
            .Include(a => a.ArticleTopics)
            .FirstOrDefaultAsync(a => a.Id == articleId, cancellationToken);

        if (targetArticle == null) return [];

        var topicIds = targetArticle.ArticleTopics.Select(at => at.TopicId).ToList();

        var query = _db.Articles
            .AsNoTracking()
            .Where(a => a.Id != articleId);

        if (topicIds.Count > 0)
        {
            var topicMatched = await query
                .Where(a => a.ArticleTopics.Any(at => topicIds.Contains(at.TopicId)))
                .OrderByDescending(a => a.PublishedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);

            if (topicMatched.Count >= limit)
            {
                return topicMatched;
            }

            var matchedIds = topicMatched.Select(m => m.Id).ToHashSet();
            var remaining = limit - topicMatched.Count;

            var categoryMatched = await query
                .Where(a => !matchedIds.Contains(a.Id) && a.Category == targetArticle.Category)
                .OrderByDescending(a => a.PublishedAt)
                .Take(remaining)
                .ToListAsync(cancellationToken);

            topicMatched.AddRange(categoryMatched);
            return topicMatched;
        }

        return await query
            .Where(a => a.Category == targetArticle.Category)
            .OrderByDescending(a => a.PublishedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureEmbeddingsAsync(DateTime cutoff, CancellationToken cancellationToken)
    {
        var unEmbedded = await _db.Articles
            .AsNoTracking()
            .Where(a => a.PublishedAt >= cutoff && !_db.ArticleEmbeddings.Any(ae => ae.ArticleId == a.Id))
            .OrderByDescending(a => a.PublishedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (unEmbedded.Count == 0) return;

        _logger.LogInformation("Generating embeddings for {Count} un-embedded articles...", unEmbedded.Count);

        var texts = unEmbedded.Select(a =>
        {
            var text = a.Title;
            if (!string.IsNullOrWhiteSpace(a.Summary))
            {
                text += ". " + a.Summary;
            }
            return text;
        }).ToList();

        var vectors = await _embeddingProvider.GenerateEmbeddingsBatchAsync(texts, cancellationToken);

        for (int i = 0; i < unEmbedded.Count && i < vectors.Count; i++)
        {
            var art = unEmbedded[i];
            var vec = vectors[i];
            var embedding = new ArticleEmbedding
            {
                ArticleId = art.Id,
                Dimensions = vec.Length,
                Vector = vec,
                Model = _embeddingProvider is LocalFallbackEmbeddingProvider ? "local-murmur3-768" : "gemini-embedding-001",
                CreatedAt = DateTime.UtcNow
            };
            _db.ArticleEmbeddings.Add(embedding);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static float ComputeTitleKeywordBoost(string? titleA, string? titleB)
    {
        if (string.IsNullOrWhiteSpace(titleA) || string.IsNullOrWhiteSpace(titleB))
            return 0f;

        var tokensA = ExtractSignificantTokens(titleA);
        var tokensB = ExtractSignificantTokens(titleB);
        if (tokensA.Count == 0 || tokensB.Count == 0)
            return 0f;

        int overlap = 0;
        foreach (var t in tokensA)
        {
            if (tokensB.Contains(t))
                overlap++;
        }

        if (overlap >= 3) return 0.28f;
        if (overlap == 2) return 0.16f;
        return 0f;
    }

    private static HashSet<string> ExtractSignificantTokens(string text)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var span = text.AsSpan();
        int start = -1;
        for (int i = 0; i <= span.Length; i++)
        {
            bool isAlphaNum = i < span.Length && char.IsLetterOrDigit(span[i]);
            if (isAlphaNum)
            {
                if (start < 0) start = i;
            }
            else if (start >= 0)
            {
                int len = i - start;
                if (len >= 3)
                {
                    var token = span.Slice(start, len).ToString();
                    if (!StopWords.Contains(token))
                    {
                        set.Add(token);
                    }
                }
                start = -1;
            }
        }
        return set;
    }

    private sealed class ClusterCandidate
    {
        public List<(Article Article, float[] Vector, float Similarity)> Items { get; } = [];
        public float[] Centroid { get; private set; }
        public HashSet<string> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ClusterCandidate(Article initialArticle, float[] initialVector)
        {
            Centroid = (float[])initialVector.Clone();
            Items.Add((initialArticle, initialVector, 1.0f));
            if (!string.IsNullOrWhiteSpace(initialArticle.Source))
            {
                Sources.Add(initialArticle.Source);
            }
        }

        public void Add(Article article, float[] vector, float similarity)
        {
            Items.Add((article, vector, similarity));
            if (!string.IsNullOrWhiteSpace(article.Source))
            {
                Sources.Add(article.Source);
            }

            // Update Centroid: running average and re-normalize
            int count = Items.Count;
            for (int i = 0; i < Centroid.Length && i < vector.Length; i++)
            {
                Centroid[i] = (Centroid[i] * (count - 1) + vector[i]) / count;
            }
            VectorMath.Normalize(Centroid);
        }
    }
}
