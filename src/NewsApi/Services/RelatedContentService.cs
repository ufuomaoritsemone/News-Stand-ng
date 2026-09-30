using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Models;

namespace NewsApi.Services;

public class RelatedStoriesDto
{
    public List<ArticleDto> Articles { get; set; } = [];
    public List<VideoStoryDto> Videos { get; set; } = [];
    public List<SocialPostDto> SocialPosts { get; set; } = [];
    public int TotalCount => Articles.Count + Videos.Count + SocialPosts.Count;
}

public class RelatedContentService : IRelatedContentService
{
    private readonly NewsDbContext _db;
    private readonly ILogger<RelatedContentService> _logger;

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "about", "above", "after", "again", "against", "all", "am", "an", "and", "any", "are", "aren't",
        "as", "at", "be", "because", "been", "before", "being", "below", "between", "both", "but", "by",
        "can't", "cannot", "could", "couldn't", "did", "didn't", "do", "does", "doesn't", "doing", "don't",
        "down", "during", "each", "few", "for", "from", "further", "had", "hadn't", "has", "hasn't", "have",
        "haven't", "having", "he", "he'd", "he'll", "he's", "her", "here", "here's", "hers", "herself", "him",
        "himself", "his", "how", "how's", "i", "i'd", "i'll", "i'm", "i've", "if", "in", "into", "is", "isn't",
        "it", "it's", "its", "itself", "let's", "me", "more", "most", "mustn't", "my", "myself", "no", "nor",
        "not", "of", "off", "on", "once", "only", "or", "other", "ought", "our", "ours", "ourselves", "out",
        "over", "own", "same", "shan't", "she", "she'd", "she'll", "she's", "should", "shouldn't", "so", "some",
        "such", "than", "that", "that's", "the", "their", "theirs", "them", "themselves", "then", "there",
        "there's", "these", "they", "they'd", "they'll", "they're", "they've", "this", "those", "through", "to",
        "too", "under", "until", "up", "very", "was", "wasn't", "we", "we'd", "we'll", "we're", "we've", "were",
        "weren't", "what", "what's", "when", "when's", "where", "where's", "which", "while", "who", "who's",
        "whom", "why", "why's", "with", "won't", "would", "wouldn't", "you", "you'd", "you'll", "you're",
        "you've", "your", "yours", "yourself", "yourselves", "news", "says", "said", "just", "breaking",
        "report", "reports", "today", "yesterday", "nigeria", "nigerian", "watch", "video", "live", "full",
        "exclusive", "latest", "update", "updates", "state", "federal", "national", "government"
    };

    public RelatedContentService(NewsDbContext db, ILogger<RelatedContentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Extracts meaningful keyword tokens from the given text (filtered by length and stop words).
    /// </summary>
    public List<string> ExtractKeywords(string? title, string? summary = null)
    {
        var rawText = $"{title} {summary}";
        if (string.IsNullOrWhiteSpace(rawText))
            return [];

        // Remove punctuation and split words
        var words = Regex.Matches(rawText, @"\b[a-zA-Z]{3,}\b")
            .Select(m => m.Value.ToLowerInvariant())
            .Where(w => !StopWords.Contains(w))
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(8)
            .ToList();

        return words;
    }

    /// <summary>
    /// Queries related articles, videos, and social posts across the SQLite database.
    /// </summary>
    public async Task<RelatedStoriesDto> GetRelatedStoriesAsync(
        string? articleId,
        string? title,
        string? category,
        int limitPerType = 4,
        CancellationToken cancellationToken = default)
    {
        var result = new RelatedStoriesDto();

        try
        {
            // If articleId is supplied, lookup article details if title/category are missing
            if (!string.IsNullOrWhiteSpace(articleId) && (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(category)))
            {
                var target = await _db.Articles.AsNoTracking()
                    .Where(a => a.Id == articleId)
                    .Select(a => new { a.Title, a.Category })
                    .FirstOrDefaultAsync(cancellationToken);

                if (target != null)
                {
                    title ??= target.Title;
                    category ??= target.Category;
                }
            }

            var keywords = ExtractKeywords(title);
            var categoryNorm = category?.Trim().ToLowerInvariant() ?? string.Empty;

            _logger.LogInformation("Finding related stories for title '{Title}' with keywords [{Keywords}] in category '{Category}'",
                title, string.Join(", ", keywords), category);

            // 1. Related Archive Articles (projecting only metadata, leaving heavy Content blob on disk)
            var articlesQuery = _db.Articles.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(articleId))
            {
                articlesQuery = articlesQuery.Where(a => a.Id != articleId);
            }
            if (!string.IsNullOrWhiteSpace(title))
            {
                var titleLower = title.Trim().ToLowerInvariant();
                articlesQuery = articlesQuery.Where(a => a.Title.ToLower() != titleLower);
            }

            var allCandidateArticles = await articlesQuery
                .OrderByDescending(a => a.PublishedAt ?? DateTime.MinValue)
                .Take(100)
                .Select(a => new
                {
                    a.Id,
                    a.Title,
                    a.Summary,
                    a.Category,
                    a.PublishedAt,
                    a.ImageUrl,
                    a.Source,
                    a.Url,
                    a.AudioUrl
                })
                .ToListAsync(cancellationToken);

            var rankedArticles = allCandidateArticles
                .Select(a =>
                {
                    int score = 0;
                    var artCat = a.Category?.ToLowerInvariant() ?? "";
                    if (!string.IsNullOrEmpty(categoryNorm) && artCat == categoryNorm)
                        score += 3;

                    var artTitle = a.Title.ToLowerInvariant();
                    var artSummary = (a.Summary ?? "").ToLowerInvariant();

                    foreach (var kw in keywords)
                    {
                        if (artTitle.Contains(kw)) score += 4;
                        else if (artSummary.Contains(kw)) score += 2;
                    }

                    return new { Article = a, Score = score };
                })
                .Where(x => x.Score > 0 || string.IsNullOrEmpty(categoryNorm))
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Article.PublishedAt ?? DateTime.MinValue)
                .Take(limitPerType)
                .Select(x => new ArticleDto
                {
                    Id = x.Article.Id,
                    Title = x.Article.Title,
                    Summary = x.Article.Summary,
                    Content = null,
                    Url = x.Article.Url,
                    ImageUrl = CategoryImageMap.Resolve(x.Article.ImageUrl, x.Article.Category),
                    Source = string.IsNullOrWhiteSpace(x.Article.Source) ? "General News" : x.Article.Source,
                    Category = string.IsNullOrWhiteSpace(x.Article.Category) ? "General" : x.Article.Category,
                    PublishedAt = x.Article.PublishedAt ?? DateTime.UtcNow,
                    AudioUrl = x.Article.AudioUrl
                })
                .ToList();

            result.Articles = rankedArticles;

            // 2. Related Video Stories
            var candidateVideos = await _db.VideoStories.AsNoTracking()
                .OrderByDescending(v => v.PublishedAt)
                .Take(60)
                .ToListAsync(cancellationToken);

            var rankedVideos = candidateVideos
                .Select(v =>
                {
                    int score = 0;
                    var vCat = v.Category.ToLowerInvariant();
                    if (!string.IsNullOrEmpty(categoryNorm) && vCat == categoryNorm)
                        score += 3;

                    var vTitle = v.Title.ToLowerInvariant();
                    var vSummary = (v.Summary ?? "").ToLowerInvariant();
                    var vChannel = v.ChannelName.ToLowerInvariant();

                    foreach (var kw in keywords)
                    {
                        if (vTitle.Contains(kw)) score += 4;
                        else if (vSummary.Contains(kw)) score += 2;
                        else if (vChannel.Contains(kw)) score += 1;
                    }

                    return new { Video = v, Score = score };
                })
                .Where(x => x.Score > 0 || string.IsNullOrEmpty(categoryNorm))
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Video.PublishedAt)
                .Take(limitPerType)
                .Select(x => new VideoStoryDto
                {
                    Id = x.Video.Id,
                    VideoId = x.Video.VideoId,
                    Title = x.Video.Title,
                    Summary = x.Video.Summary,
                    VideoUrl = x.Video.VideoUrl,
                    ThumbnailUrl = x.Video.ThumbnailUrl,
                    ChannelName = x.Video.ChannelName,
                    ChannelId = x.Video.ChannelId,
                    Duration = x.Video.Duration,
                    Category = x.Video.Category,
                    PublishedAt = x.Video.PublishedAt
                })
                .ToList();

            result.Videos = rankedVideos;

            // 3. Related Social Posts / Tweets
            var candidatePosts = await _db.SocialPosts.AsNoTracking()
                .OrderByDescending(s => s.PublishedAt)
                .Take(60)
                .ToListAsync(cancellationToken);

            var rankedPosts = candidatePosts
                .Select(s =>
                {
                    int score = 0;
                    var sCat = s.Category.ToLowerInvariant();
                    if (!string.IsNullOrEmpty(categoryNorm) && sCat == categoryNorm)
                        score += 3;

                    var sContent = s.Content.ToLowerInvariant();
                    var sAuthor = s.AuthorName.ToLowerInvariant();
                    var sHandle = s.AuthorHandle.ToLowerInvariant();

                    foreach (var kw in keywords)
                    {
                        if (sContent.Contains(kw)) score += 4;
                        else if (sAuthor.Contains(kw) || sHandle.Contains(kw)) score += 1;
                    }

                    return new { Post = s, Score = score };
                })
                .Where(x => x.Score > 0 || string.IsNullOrEmpty(categoryNorm))
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Post.PublishedAt)
                .Take(limitPerType)
                .Select(x => new SocialPostDto
                {
                    Id = x.Post.Id,
                    AuthorName = x.Post.AuthorName,
                    AuthorHandle = x.Post.AuthorHandle,
                    AuthorAvatarUrl = x.Post.AuthorAvatarUrl,
                    Content = x.Post.Content,
                    PostUrl = x.Post.PostUrl,
                    MediaUrl = x.Post.MediaUrl,
                    Category = x.Post.Category,
                    LikesCount = x.Post.LikesCount,
                    RetweetsCount = x.Post.RetweetsCount,
                    PublishedAt = x.Post.PublishedAt
                })
                .ToList();

            result.SocialPosts = rankedPosts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing related stories");
        }

        return result;
    }
}
