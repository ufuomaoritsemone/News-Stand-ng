using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Models;
using NigerianNewsGrid.Client.Helpers;

namespace NewsApi.Services;

/// <summary>
/// Default implementation of <see cref="IBriefingService"/>.
/// Handles freshness windowing, story deduplication, enrichment hydration, and token-based cache eviction.
/// </summary>
public class BriefingService : IBriefingService
{
    private static CancellationTokenSource _briefingTokenSource = new();
    private const string BriefingCacheTag = "briefings";

    private readonly NewsDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger _logger;

    public BriefingService(
        NewsDbContext db,
        IMemoryCache cache,
        ILogger<BriefingService> logger)
        : this(db, cache, (ILogger)logger)
    {
    }

    public BriefingService(
        NewsDbContext db,
        IMemoryCache cache,
        ILogger logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<BriefingResult> GetBriefingsAsync(
        int topPerCategory = 5,
        string? language = null,
        bool isInternal = false,
        CancellationToken cancellationToken = default)
    {
        var preferredLanguage = string.IsNullOrWhiteSpace(language) ? "English" : language.Trim();
        var cacheKey = $"{BriefingCacheTag}_{preferredLanguage}_{topPerCategory}";

        if (_cache.TryGetValue(cacheKey, out List<BriefingCategoryDto>? cached) && cached != null)
        {
            return new BriefingResult(cached, CacheHit: true, HoursStale: 0);
        }

        _logger.LogInformation(
            "GetBriefings cache miss. Building briefing: topPerCategory={Top}, language={Lang}",
            topPerCategory, preferredLanguage);

        // Freshness cutoff — avoid scanning entire table into memory
        var cutoff = DateTime.UtcNow.AddDays(-3);
        var articles = await _db.Articles
            .AsNoTracking()
            .Where(a => a.PublishedAt >= cutoff || (a.IsSponsored && (a.CampaignExpiresAt == null || a.CampaignExpiresAt > DateTime.UtcNow)))
            .OrderByDescending(a => a.IsPinned && a.IsSponsored && (a.CampaignExpiresAt == null || a.CampaignExpiresAt > DateTime.UtcNow))
            .ThenByDescending(a => a.PublishedAt)
            .ToListAsync(cancellationToken);

        // Fallback to most-recent 200 if recent window is sparse
        if (articles.Count == 0)
        {
            articles = await _db.Articles
                .AsNoTracking()
                .OrderByDescending(a => a.IsPinned && a.IsSponsored && (a.CampaignExpiresAt == null || a.CampaignExpiresAt > DateTime.UtcNow))
                .ThenByDescending(a => a.PublishedAt ?? DateTime.MinValue)
                .Take(200)
                .ToListAsync(cancellationToken);
        }

        if (articles.Count == 0)
        {
            _logger.LogWarning("No articles found in database.");
            return new BriefingResult(new List<BriefingCategoryDto>(), CacheHit: false, HoursStale: 0);
        }

        var topArticlesByCategory = articles
            .GroupBy(a => string.IsNullOrWhiteSpace(a.Category) ? "General" : a.Category)
            .Select(g => new
            {
                Category = g.Key,
                Articles = StoryDeduplicationHelper.DeduplicateStories(g, a => a.Title, a => a.Summary, a => a.Source)
                    .Take(topPerCategory)
                    .ToList()
            })
            .ToList();

        var topArticleIds = topArticlesByCategory
            .SelectMany(g => g.Articles)
            .Select(a => a.Id)
            .Distinct()
            .ToList();

        var enrichmentMap = topArticleIds.Count > 0
            ? await _db.ArticleEnrichments.AsNoTracking()
                .Where(e => topArticleIds.Contains(e.ArticleId))
                .ToDictionaryAsync(e => e.ArticleId, cancellationToken)
            : new Dictionary<string, ArticleEnrichment>();

        var commentCounts = topArticleIds.Count > 0
            ? await _db.StoryComments.AsNoTracking()
                .Where(c => topArticleIds.Contains(c.ArticleId) && c.Status != "Hidden")
                .GroupBy(c => c.ArticleId)
                .Select(g => new { ArticleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ArticleId, x => x.Count, cancellationToken)
            : new Dictionary<string, int>();

        var briefing = topArticlesByCategory
            .Select(g => new BriefingCategoryDto
            {
                Category = g.Category,
                Top = g.Articles
                    .Select(a =>
                    {
                        var enr = enrichmentMap.GetValueOrDefault(a.Id);
                        var aiSummary = enr?.FullSummary?.ExecutiveOverview ?? enr?.KeyTakeaways.FirstOrDefault();
                        return new BriefingItemDto
                        {
                            Id              = a.Id,
                            Title           = a.Title,
                            Summary         = a.Summary,
                            Content         = isInternal ? a.Content : null,
                            Url             = a.Url,
                            ImageUrl        = CategoryImageMap.Resolve(a.ImageUrl, a.Category),
                            Source          = a.Source ?? "General News",
                            Category        = a.Category ?? "General",
                            PublishedAt     = a.PublishedAt,
                            AudioUrl        = a.AudioUrl,
                            Language        = preferredLanguage,
                            VoiceName       = preferredLanguage switch
                            {
                                "Yoruba" => "Yoruba Female",
                                "Igbo"   => "Igbo Female",
                                "Hausa"  => "Hausa Female",
                                _        => "English Female"
                            },
                            Author          = a.Author,
                            ContentType     = string.IsNullOrWhiteSpace(a.ContentType) ? "News" : a.ContentType,
                            Slug            = a.Slug,
                            IsSponsored     = a.IsSponsored && (a.CampaignExpiresAt == null || a.CampaignExpiresAt > DateTime.UtcNow),
                            SponsorName     = a.SponsorName,
                            SponsorUrl      = a.SponsorUrl,
                            IsPinned        = a.IsPinned,
                            TargetPosition  = a.TargetPosition,
                            PriorityWeight  = a.PriorityWeight,
                            PlacementZone   = a.PlacementZone ?? "MainPage_InFeed",
                            TargetPlatform  = a.TargetPlatform ?? "All",
                            HasAiSummary    = enr != null,
                            AiSummary       = aiSummary,
                            AiSummaryEngine = enr?.Engine,
                            KeyTakeaways    = enr?.KeyTakeaways ?? new List<string>(),
                            CommentCount    = commentCounts.GetValueOrDefault(a.Id, 0)
                        };
                    }).ToList()
            })
            .ToList();

        _cache.Set(cacheKey, briefing, new MemoryCacheEntryOptions()
            .AddExpirationToken(new CancellationChangeToken(_briefingTokenSource.Token))
            .SetSlidingExpiration(TimeSpan.FromMinutes(3))
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(5)));

        var latestArticle = articles.FirstOrDefault();
        var hoursStale = latestArticle?.PublishedAt is { } pub
            ? (DateTime.UtcNow - pub).TotalHours
            : 0;

        return new BriefingResult(briefing, CacheHit: false, HoursStale: hoursStale);
    }

    public async Task<BriefingAvailabilityResult> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-3);
        var count = await _db.Articles.AsNoTracking().CountAsync(a => a.PublishedAt >= cutoff, cancellationToken);
        if (count == 0)
        {
            count = await _db.Articles.AsNoTracking().CountAsync(cancellationToken);
        }

        return new BriefingAvailabilityResult(
            Available: count > 0,
            ArticleCount: count,
            TimestampUtc: DateTime.UtcNow);
    }

    public void InvalidateBriefingCache()
    {
        var oldToken = Interlocked.Exchange(ref _briefingTokenSource, new CancellationTokenSource());
        try
        {
            oldToken.Cancel();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cancelling briefing change token.");
        }
        finally
        {
            oldToken.Dispose();
        }
    }
}
