using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Models;
using NewsApi.Services;
using NigerianNewsGrid.Client.Helpers;
using NigerianNewsGrid.Client.Models;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ArticlesController : ControllerBase
{
    private static CancellationTokenSource _briefingTokenSource = new();

    private readonly NewsDbContext _db;
    private readonly IRelatedContentService _relatedService; // Fix #19 — abstraction
    private readonly IArticleSearchService _searchService;
    private readonly ICategorizerTrainingService? _categorizerTrainingService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ArticlesController> _logger;

    private const string BriefingCacheTag = "briefings";

    public ArticlesController(
        NewsDbContext db,
        IRelatedContentService relatedService,  // Fix #19
        IArticleSearchService searchService,
        IMemoryCache cache,
        ILogger<ArticlesController> logger,
        ICategorizerTrainingService? categorizerTrainingService = null)
    {
        _db                         = db;
        _relatedService             = relatedService;
        _searchService              = searchService;
        _cache                      = cache;
        _logger                     = logger;
        _categorizerTrainingService = categorizerTrainingService;
    }

    /// <summary>
    /// Returns related archive articles, video stories, and tweets based on keyword/entity and category matching.
    /// </summary>
    [HttpGet("related")]
    public async Task<IActionResult> GetRelatedStories(
        [FromQuery] string? id       = null,
        [FromQuery] string? title    = null,
        [FromQuery] string? category = null,
        [FromQuery] int     limit    = 4,
        CancellationToken   cancellationToken = default)
    {
        var effectiveLimit = Math.Clamp(limit, 1, 20);
        var result = await _relatedService.GetRelatedStoriesAsync(id, title, category, effectiveLimit, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns articles organised by category for the mobile briefing UI,
    /// with in-memory caching and freshness windowing.
    /// Fix #27: no raw ex.Message in responses.
    /// Fix #28: removed redundant try/catch (GlobalExceptionMiddleware handles unhandled exceptions).
    /// </summary>
    [HttpGet("briefings")]
    public async Task<IActionResult> GetBriefings(
        [FromQuery] int    topPerCategory = 5,
        [FromQuery] string? language      = null,
        CancellationToken  cancellationToken = default)
    {
        var preferredLanguage = string.IsNullOrWhiteSpace(language) ? "English" : language.Trim();
        var cacheKey = $"{BriefingCacheTag}_{preferredLanguage}_{topPerCategory}";

        if (_cache.TryGetValue(cacheKey, out List<BriefingCategoryDto>? cached) && cached != null)
        {
            Response.Headers.Append("X-Cache", "HIT");
            return Ok(cached);
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
            return Ok(new List<BriefingCategoryDto>());
        }

        var briefing = articles
            .GroupBy(a => string.IsNullOrWhiteSpace(a.Category) ? "General" : a.Category)
            .Select(g => new BriefingCategoryDto
            {
                Category = g.Key,
                Top = StoryDeduplicationHelper.DeduplicateStories(g, a => a.Title, a => a.Summary, a => a.Source)
                    .Take(topPerCategory)
                    .Select(a => new BriefingItemDto
                    {
                        Id          = a.Id,
                        Title       = a.Title,
                        Summary     = a.Summary,
                        Content     = a.Content,
                        Url         = a.Url,
                        ImageUrl    = CategoryImageMap.Resolve(a.ImageUrl, a.Category), // Fix #23
                        Source      = a.Source ?? "General News",
                        Category    = a.Category ?? "General",
                        PublishedAt = a.PublishedAt ?? DateTime.UtcNow,
                        AudioUrl    = a.AudioUrl,
                        Language    = preferredLanguage,
                        VoiceName   = preferredLanguage switch
                        {
                            "Yoruba" => "Yoruba Female",
                            "Igbo"   => "Igbo Female",
                            "Hausa"  => "Hausa Female",
                            _        => "English Female"
                        },
                        Author      = a.Author,
                        ContentType = string.IsNullOrWhiteSpace(a.ContentType) ? "News" : a.ContentType,
                        IsSponsored = a.IsSponsored && (a.CampaignExpiresAt == null || a.CampaignExpiresAt > DateTime.UtcNow),
                        SponsorName = a.SponsorName,
                        SponsorUrl  = a.SponsorUrl,
                        IsPinned    = a.IsPinned,
                        TargetPosition = a.TargetPosition,
                        PriorityWeight = a.PriorityWeight
                    }).ToList()
            })
            .ToList();

        _cache.Set(cacheKey, briefing, new MemoryCacheEntryOptions()
            .AddExpirationToken(new CancellationChangeToken(_briefingTokenSource.Token))
            .SetSlidingExpiration(TimeSpan.FromMinutes(3))
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(5)));

        var latestArticle = articles.FirstOrDefault();
        var hoursStale    = latestArticle?.PublishedAt is { } pub
            ? (DateTime.UtcNow - pub).TotalHours
            : 0;

        Response.Headers.Append("X-Data-Age-Hours", hoursStale.ToString("F1"));
        Response.Headers.Append("X-Cache", "MISS");

        return Ok(briefing);
    }

    /// <summary>
    /// Checks whether curated audio headlines / daily briefing articles are available.
    /// Lightweight endpoint for background notification schedulers and client readiness checks.
    /// </summary>
    [HttpGet("briefings/availability")]
    public async Task<IActionResult> CheckBriefingAvailability(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-3);
        var count = await _db.Articles.AsNoTracking().CountAsync(a => a.PublishedAt >= cutoff, cancellationToken);
        if (count == 0)
        {
            count = await _db.Articles.AsNoTracking().CountAsync(cancellationToken);
        }

        var available = count > 0;
        return Ok(new
        {
            available,
            articleCount = count,
            timestampUtc = DateTime.UtcNow
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetArticles(
        [FromQuery] string? source,
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] string? contentType = null,
        [FromQuery] bool    todayOnly  = false,
        [FromQuery] int?    days       = null,
        [FromQuery] int     limit      = 200,
        [FromQuery] int     page       = 1,
        [FromQuery] int     pageSize   = 50,
        CancellationToken   cancellationToken = default)
    {
        var query = _db.Articles.AsNoTracking();

        if (todayOnly)
        {
            var today = DateTime.UtcNow.Date;
            query = query.Where(a => a.PublishedAt != null && a.PublishedAt >= today);
        }
        else if (days.HasValue && days.Value > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days.Value);
            query = query.Where(a => a.PublishedAt != null && a.PublishedAt >= cutoff);
        }

        if (!string.IsNullOrWhiteSpace(source) && !source.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var s = source.Trim().ToLowerInvariant();
            query = query.Where(a => a.Source != null && a.Source.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var c = category.Trim().ToLowerInvariant();
            query = query.Where(a => a.Category != null && a.Category.ToLower() == c);
        }

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            var ct = contentType.Trim().ToLowerInvariant();
            query = query.Where(a => a.ContentType != null && a.ContentType.ToLower() == ct);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchParams = new ArticleSearchParams(
                Search: search,
                Source: source,
                Category: category,
                TodayOnly: todayOnly,
                Days: days,
                Page: page,
                PageSize: pageSize > 0 ? pageSize : limit,
                ContentType: contentType
            );

            var searchResults = await _searchService.SearchArticlesAsync(searchParams, cancellationToken);
            return Ok(searchResults);
        }

        var pageIndex = Math.Max(1, page);
        var size      = Math.Clamp(pageSize > 0 ? pageSize : limit, 1, 200);

        var rawArticles = await query
            .OrderByDescending(a => a.PublishedAt ?? DateTime.MinValue)
            .Skip((pageIndex - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        // Fix #11 — only fetch audio assets for the articles on this page, not the entire table
        var pageIds  = rawArticles.Select(a => a.Id).ToList();
        var audioMap = pageIds.Count > 0
            ? (await _db.AudioAssets.AsNoTracking()
                .Where(x => x.ArticleId != null && pageIds.Contains(x.ArticleId))
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.ArticleId!)
                .ToDictionary(g => g.Key, g => g.First().Url)
            : new Dictionary<string, string>();

        var articleDtos = rawArticles.Select(a => new ArticleDto
        {
            Id          = a.Id,
            Title       = a.Title,
            Summary     = a.Summary,
            Content     = a.Content,
            Url         = a.Url,
            ImageUrl    = CategoryImageMap.Resolve(a.ImageUrl, a.Category), // Fix #23
            Source      = string.IsNullOrWhiteSpace(a.Source)   ? "General News" : a.Source,
            Category    = string.IsNullOrWhiteSpace(a.Category) ? "General"      : a.Category,
            PublishedAt = a.PublishedAt ?? DateTime.UtcNow,
            AudioUrl    = audioMap.GetValueOrDefault(a.Id) ?? a.AudioUrl,
            Author      = a.Author,
            ContentType = string.IsNullOrWhiteSpace(a.ContentType) ? "News" : a.ContentType,
            IsSponsored = a.IsSponsored,
            SponsorName = a.SponsorName,
            SponsorUrl  = a.SponsorUrl,
            CampaignExpiresAt = a.CampaignExpiresAt,
            IsPinned    = a.IsPinned,
            ImpressionCount = a.ImpressionCount,
            ClickCount  = a.ClickCount
        }).ToList();

        return Ok(articleDtos);
    }

    /// <summary>
    /// Returns a single article by ID with full content body, image, and metadata.
    /// Used for push notifications, deep links, and native reader hydration.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetArticleById(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "Article ID cannot be empty." });

        var a = await _db.Articles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (a == null)
            return NotFound(new { message = $"Article with ID '{id}' was not found." });

        var audio = await _db.AudioAssets
            .AsNoTracking()
            .Where(x => x.ArticleId == a.Id)
            .Select(x => x.Url)
            .FirstOrDefaultAsync(cancellationToken);

        var dto = new ArticleDto
        {
            Id          = a.Id,
            Title       = a.Title,
            Summary     = a.Summary,
            Content     = a.Content,
            Url         = a.Url,
            ImageUrl    = CategoryImageMap.Resolve(a.ImageUrl, a.Category),
            Source      = string.IsNullOrWhiteSpace(a.Source)   ? "General News" : a.Source,
            Category    = string.IsNullOrWhiteSpace(a.Category) ? "General"      : a.Category,
            PublishedAt = a.PublishedAt ?? DateTime.UtcNow,
            AudioUrl    = audio ?? a.AudioUrl,
            Author      = a.Author,
            ContentType = string.IsNullOrWhiteSpace(a.ContentType) ? "News" : a.ContentType,
            IsSponsored = a.IsSponsored,
            SponsorName = a.SponsorName,
            SponsorUrl  = a.SponsorUrl,
            CampaignExpiresAt = a.CampaignExpiresAt,
            IsPinned    = a.IsPinned,
            ImpressionCount = a.ImpressionCount,
            ClickCount  = a.ClickCount
        };

        return Ok(dto);
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> IngestArticles(
        [FromBody] IngestArticlesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request?.Articles == null || !request.Articles.Any())
            return BadRequest(new { message = "No articles provided in payload." });

        var incomingList = request.Articles
            .Where(i => !string.IsNullOrWhiteSpace(i.Title))
            .ToList();

        if (!incomingList.Any())
            return Ok(new IngestArticlesResponse { IngestedCount = 0, SkippedDuplicateCount = 0 });

        // Batch duplicate check — O(1) HashSet lookups instead of N DB round-trips
        var incomingUrls = incomingList
            .Select(a => a.Url?.Trim())
            .Where(u => !string.IsNullOrEmpty(u))
            .ToList();

        var existingUrls = incomingUrls.Count > 0
            ? (await _db.Articles
                .AsNoTracking()
                .Where(a => a.Url != null && incomingUrls.Contains(a.Url))
                .Select(a => a.Url!)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var incomingTitles = incomingList
            .Select(a => a.Title.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var incomingTitlesLower = incomingTitles
            .Select(t => t.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Problem 1 Fix: Targeted duplicate title lookup — queries ONLY incoming titles matching the batch
        // instead of loading all articles from the past 14 days into server memory.
        var existingTitles = incomingTitles.Count > 0
            ? (await _db.Articles
                .AsNoTracking()
                .Where(a => incomingTitles.Contains(a.Title) || incomingTitlesLower.Contains(a.Title.ToLower()))
                .Select(a => a.Title)
                .ToListAsync(cancellationToken))
                .Select(t => t.Trim().ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int ingested = 0, skipped = 0;

        foreach (var item in incomingList)
        {
            var titleNorm = item.Title.Trim().ToLowerInvariant();
            var urlNorm   = item.Url?.Trim();

            bool isDuplicate = (!string.IsNullOrEmpty(urlNorm) && existingUrls.Contains(urlNorm)) ||
                               existingTitles.Contains(titleNorm);

            if (isDuplicate) { skipped++; continue; }

            _db.Articles.Add(new Article
            {
                Id          = Guid.NewGuid().ToString("N"),
                Title       = item.Title.Trim(),
                Summary     = item.Summary,
                Content     = item.Content,
                Url         = item.Url,
                ImageUrl    = item.ImageUrl,
                Source      = item.Source ?? "Scraped News",
                Category    = item.Category ?? "General",
                PublishedAt = item.PublishedAt ?? DateTime.UtcNow,
                AudioUrl    = item.AudioUrl,
                Author      = item.Author,
                ContentType = string.IsNullOrWhiteSpace(item.ContentType) ? "News" : item.ContentType
            });

            if (!string.IsNullOrEmpty(urlNorm)) existingUrls.Add(urlNorm);
            existingTitles.Add(titleNorm);
            ingested++;
        }

        if (ingested > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            InvalidateBriefingCache(); // Fix #31
        }

        _logger.LogInformation(
            "Ingested {IngestedCount} articles, skipped {SkippedCount} duplicates.",
            ingested, skipped);

        return Ok(new IngestArticlesResponse
        {
            IngestedCount        = ingested,
            SkippedDuplicateCount = skipped
        });
    }

    [HttpPut("{id}/category")]
    public async Task<IActionResult> UpdateCategory(
        string id,
        [FromBody] UpdateArticleCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Category))
            return BadRequest(new { message = "Category is required." });

        var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article == null)
            return NotFound(new { message = $"Article with ID '{id}' was not found." });

        var oldCategory    = article.Category ?? "General";
        article.Category   = request.Category.Trim();
        article.UpdatedAt  = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await SaveCorrectionFeedbackAsync(article, oldCategory, cancellationToken);
        InvalidateBriefingCache(); // Fix #31

        _logger.LogInformation(
            "Updated category for article '{ArticleId}' from '{Old}' to '{New}'.",
            id, oldCategory, article.Category);

        return Ok(new { id = article.Id, category = article.Category, message = "Category updated successfully." });
    }

    /// <summary>
    /// Fetches lightweight article delta records (e.g. category reclassifications) modified since the specified UTC timestamp.
    /// Enables client-side SQLite caches to synchronize out-of-band updates with minimal bandwidth.
    /// </summary>
    [HttpGet("sync")]
    public async Task<IActionResult> GetArticleDeltas(
        [FromQuery] DateTime? sinceUtc,
        CancellationToken cancellationToken = default)
    {
        var cutoff = sinceUtc ?? DateTime.UtcNow.AddDays(-14);

        var deltas = await _db.Articles
            .AsNoTracking()
            .Where(a => a.UpdatedAt != null && a.UpdatedAt > cutoff)
            .OrderBy(a => a.UpdatedAt)
            .Take(500)
            .Select(a => new ArticleDeltaDto
            {
                Id = a.Id,
                Category = a.Category ?? "General",
                UpdatedAt = a.UpdatedAt!.Value
            })
            .ToListAsync(cancellationToken);

        return Ok(deltas);
    }

    // Atomic briefing cache eviction via CancellationChangeToken
    private void InvalidateBriefingCache()
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

    private async Task SaveCorrectionFeedbackAsync(
        Article article, string oldCategory, CancellationToken ct)
    {
        try
        {
            _db.CategoryCorrections.Add(new CategoryCorrection
            {
                ArticleId   = article.Id,
                Title       = article.Title,
                Summary     = article.Summary,
                OldCategory = oldCategory,
                NewCategory = article.Category ?? "General",
                Source      = article.Source,
                Url         = article.Url,
                CreatedAt   = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Persisted category correction for article {ArticleId}.", article.Id);

            _categorizerTrainingService?.CheckAndTriggerAutoRetrain();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save category correction for article {ArticleId}.", article.Id);
        }
    }

    // ── Direct Sponsorship Management Endpoints ──────────────────────────────

    /// <summary>
    /// Returns all direct sponsored articles and active campaigns for Admin management.
    /// </summary>
    [HttpGet("sponsored")]
    public async Task<IActionResult> GetSponsoredArticles(CancellationToken ct = default)
    {
        var sponsored = await _db.Articles
            .AsNoTracking()
            .Where(a => a.IsSponsored)
            .OrderByDescending(a => a.PublishedAt ?? DateTime.MinValue)
            .Select(a => new ArticleDto
            {
                Id          = a.Id,
                Title       = a.Title,
                Summary     = a.Summary,
                Content     = a.Content,
                Url         = a.Url,
                ImageUrl    = CategoryImageMap.Resolve(a.ImageUrl, a.Category),
                Source      = string.IsNullOrWhiteSpace(a.Source) ? $"{a.SponsorName} Press Release" : a.Source,
                Category    = string.IsNullOrWhiteSpace(a.Category) ? "Business" : a.Category,
                PublishedAt = a.PublishedAt ?? DateTime.UtcNow,
                IsSponsored = a.IsSponsored,
                SponsorName = a.SponsorName,
                SponsorUrl  = a.SponsorUrl,
                CampaignExpiresAt = a.CampaignExpiresAt,
                IsPinned    = a.IsPinned,
                TargetPosition = a.TargetPosition,
                PriorityWeight = a.PriorityWeight,
                ImpressionCount = a.ImpressionCount,
                ClickCount  = a.ClickCount
            })
            .ToListAsync(ct);

        return Ok(sponsored);
    }

    /// <summary>
    /// Creates a new in-house direct sponsored article / corporate press release.
    /// </summary>
    [HttpPost("sponsored")]
    public async Task<IActionResult> CreateSponsoredArticle(
        [FromBody] CreateSponsoredArticleRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.SponsorName))
        {
            return BadRequest(new { Message = "Title and SponsorName are required." });
        }

        if (request.TargetPosition.HasValue)
        {
            var pos = request.TargetPosition.Value;
            if (!((pos >= 1 && pos <= 3) || (pos >= 5 && pos <= 30)))
            {
                return BadRequest(new { Message = "TargetPosition must be either a Special Placement (1, 2, or 3) or an In-Feed Placement between 5 and 30." });
            }
        }

        var article = new Article
        {
            Id                = Guid.NewGuid().ToString("N"),
            Title             = request.Title.Trim(),
            Summary           = request.Summary?.Trim(),
            Content           = request.Content?.Trim(),
            Url               = !string.IsNullOrWhiteSpace(request.Url) ? request.Url.Trim() : (!string.IsNullOrWhiteSpace(request.SponsorUrl) ? request.SponsorUrl.Trim() : null),
            ImageUrl          = request.ImageUrl?.Trim(),
            Source            = $"{request.SponsorName.Trim()} (Sponsored)",
            Category          = !string.IsNullOrWhiteSpace(request.Category) ? request.Category.Trim() : "Business",
            PublishedAt       = DateTime.UtcNow,
            IsSponsored       = true,
            SponsorName       = request.SponsorName.Trim(),
            SponsorUrl        = request.SponsorUrl?.Trim(),
            CampaignExpiresAt = request.CampaignExpiresAt,
            IsPinned          = request.IsPinned,
            TargetPosition    = request.TargetPosition,
            PriorityWeight    = request.PriorityWeight > 0 ? request.PriorityWeight : 1,
            ImpressionCount   = 0,
            ClickCount        = 0
        };

        _db.Articles.Add(article);
        await _db.SaveChangesAsync(ct);
        InvalidateBriefingCache();

        _logger.LogInformation("Created new sponsored campaign '{Title}' for sponsor '{Sponsor}'.", article.Title, article.SponsorName);
        return Created($"/api/v1/articles/{article.Id}", article);
    }

    /// <summary>
    /// Updates an existing sponsored campaign.
    /// </summary>
    [HttpPut("sponsored/{id}")]
    public async Task<IActionResult> UpdateSponsoredArticle(
        string id,
        [FromBody] UpdateSponsoredArticleRequest request,
        CancellationToken ct = default)
    {
        var article = await _db.Articles.FindAsync([id], ct);
        if (article == null)
        {
            return NotFound(new { Message = $"Article with ID '{id}' was not found." });
        }

        if (!string.IsNullOrWhiteSpace(request.Title)) article.Title = request.Title.Trim();
        if (request.Summary != null) article.Summary = request.Summary.Trim();
        if (request.Content != null) article.Content = request.Content.Trim();
        if (request.Url != null) article.Url = request.Url.Trim();
        if (request.ImageUrl != null) article.ImageUrl = request.ImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(request.SponsorName)) article.SponsorName = request.SponsorName.Trim();
        if (request.SponsorUrl != null) article.SponsorUrl = request.SponsorUrl.Trim();
        if (!string.IsNullOrWhiteSpace(request.Category)) article.Category = request.Category.Trim();
        if (request.CampaignExpiresAt.HasValue) article.CampaignExpiresAt = request.CampaignExpiresAt.Value;
        if (request.IsPinned.HasValue) article.IsPinned = request.IsPinned.Value;
        if (request.IsSponsored.HasValue) article.IsSponsored = request.IsSponsored.Value;
        if (request.TargetPosition.HasValue)
        {
            var pos = request.TargetPosition.Value;
            if (!((pos >= 1 && pos <= 3) || (pos >= 5 && pos <= 30)))
            {
                return BadRequest(new { Message = "TargetPosition must be either a Special Placement (1, 2, or 3) or an In-Feed Placement between 5 and 30." });
            }
            article.TargetPosition = pos;
        }
        if (request.PriorityWeight.HasValue && request.PriorityWeight.Value > 0)
        {
            article.PriorityWeight = request.PriorityWeight.Value;
        }

        await _db.SaveChangesAsync(ct);
        InvalidateBriefingCache();

        _logger.LogInformation("Updated sponsored campaign '{Id}'.", id);
        return Ok(article);
    }

    /// <summary>
    /// Deletes or terminates a sponsored campaign.
    /// </summary>
    [HttpDelete("sponsored/{id}")]
    public async Task<IActionResult> DeleteSponsoredArticle(string id, CancellationToken ct = default)
    {
        var article = await _db.Articles.FindAsync([id], ct);
        if (article == null)
        {
            return NotFound(new { Message = $"Article with ID '{id}' was not found." });
        }

        _db.Articles.Remove(article);
        await _db.SaveChangesAsync(ct);
        InvalidateBriefingCache();

        _logger.LogInformation("Deleted sponsored campaign '{Id}'.", id);
        return NoContent();
    }

    /// <summary>
    /// Records an impression for a story/article (used for CTR tracking).
    /// Executes an atomic SQL increment to eliminate lost update race conditions under high concurrency.
    /// </summary>
    [HttpPost("{id}/track-impression")]
    public async Task<IActionResult> TrackImpression(string id, CancellationToken ct = default)
    {
        var updated = await _db.Articles
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ImpressionCount, a => a.ImpressionCount + 1), ct);

        if (updated == 0)
        {
            return NotFound(new { message = $"Article with ID '{id}' not found." });
        }

        return NoContent();
    }

    /// <summary>
    /// Records a click-through for a sponsored story or advertiser URL.
    /// Executes an atomic SQL increment to eliminate lost update race conditions under high concurrency.
    /// </summary>
    [HttpPost("{id}/track-click")]
    public async Task<IActionResult> TrackClick(string id, CancellationToken ct = default)
    {
        var updated = await _db.Articles
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ClickCount, a => a.ClickCount + 1), ct);

        if (updated == 0)
        {
            return NotFound(new { message = $"Article with ID '{id}' not found." });
        }

        return NoContent();
    }
}

public class UpdateArticleCategoryRequest
{
    public string Category { get; set; } = string.Empty;
}
