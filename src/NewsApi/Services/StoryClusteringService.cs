using Microsoft.EntityFrameworkCore;
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

    public StoryClusteringService(
        NewsDbContext db,
        IEmbeddingProvider embeddingProvider,
        ISlugService slugService,
        IConfiguration config,
        ILogger<StoryClusteringService> logger)
    {
        _db = db;
        _embeddingProvider = embeddingProvider;
        _slugService = slugService;
        _config = config;
        _logger = logger;
    }

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

        if (articlePairs.Count < 2)
        {
            _logger.LogInformation("StoryClusteringService: Insufficient articles with embeddings ({Count}) to form clusters.", articlePairs.Count);
            return 0;
        }

        var threshold = _config.GetValue<float>("Clustering:SimilarityThreshold", 0.80f);
        _logger.LogInformation("Clustering {Count} articles with cosine threshold {Threshold}", articlePairs.Count, threshold);

        // 3. Cluster articles using Greedy Leader clustering
        var candidates = new List<ClusterCandidate>();

        foreach (var item in articlePairs)
        {
            var vector = item.Embedding.Vector;
            if (vector.Length == 0) continue;

            ClusterCandidate? bestCandidate = null;
            float bestSim = 0f;

            foreach (var cand in candidates)
            {
                var sim = VectorMath.CosineSimilarity(vector, cand.Centroid);
                if (sim >= threshold && sim > bestSim)
                {
                    bestSim = sim;
                    bestCandidate = cand;
                }
            }

            if (bestCandidate != null)
            {
                bestCandidate.Add(item.Article!, vector, bestSim);
            }
            else
            {
                var newCand = new ClusterCandidate(item.Article!, vector);
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
        var targetEmbedding = await _db.ArticleEmbeddings
            .AsNoTracking()
            .FirstOrDefaultAsync(ae => ae.ArticleId == articleId, cancellationToken);

        if (targetEmbedding != null && targetEmbedding.Vector.Length > 0)
        {
            var targetVector = targetEmbedding.Vector;
            var windowCutoff = DateTime.UtcNow.AddDays(-14);

            var candidates = await _db.ArticleEmbeddings
                .AsNoTracking()
                .Include(ae => ae.Article)
                .Where(ae => ae.ArticleId != articleId && ae.Article != null && ae.Article.PublishedAt >= windowCutoff)
                .ToListAsync(cancellationToken);

            var scored = new List<(Article Article, float Score)>();
            foreach (var cand in candidates)
            {
                var vec = cand.Vector;
                if (vec.Length == targetVector.Length)
                {
                    var sim = VectorMath.CosineSimilarity(targetVector, vec);
                    if (sim >= 0.45f)
                    {
                        scored.Add((cand.Article!, sim));
                    }
                }
            }

            var topSimilar = scored
                .OrderByDescending(s => s.Score)
                .Take(limit)
                .Select(s => s.Article)
                .ToList();

            if (topSimilar.Count >= Math.Min(limit, 3))
            {
                return topSimilar;
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
        var existingEmbeddingIds = await _db.ArticleEmbeddings.Select(ae => ae.ArticleId).ToListAsync(cancellationToken);
        var unEmbedded = await _db.Articles
            .AsNoTracking()
            .Where(a => a.PublishedAt >= cutoff && !existingEmbeddingIds.Contains(a.Id))
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
                Model = "text-embedding-004",
                CreatedAt = DateTime.UtcNow
            };
            _db.ArticleEmbeddings.Add(embedding);
        }

        await _db.SaveChangesAsync(cancellationToken);
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
