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
[Route("api/[controller]")]
public class ArticlesController : ControllerBase
{
    private static CancellationTokenSource _briefingTokenSource = new();

    private readonly NewsDbContext _db;
    private readonly IRelatedContentService _relatedService; // Fix #19 — abstraction
    private readonly IArticleSearchService _searchService;
    private readonly ICategorizerTrainingService? _categorizerTrainingService;
    private readonly ISlugService _slugService;
    private readonly IArticleSeoBackfillService? _seoBackfillService;
    private readonly IStoryClusteringService? _clusteringService;
    private readonly IArticleEnrichmentService? _enrichmentService;
    private readonly IIndexNowService? _indexNowService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ArticlesController> _logger;

    private readonly IConfiguration? _config;

    private const string BriefingCacheTag = "briefings";

    public ArticlesController(
        NewsDbContext db,
        IRelatedContentService relatedService,  // Fix #19
        IArticleSearchService searchService,
        IMemoryCache cache,
        ILogger<ArticlesController> logger,
        ISlugService slugService,
        IArticleSeoBackfillService? seoBackfillService = null,
        ICategorizerTrainingService? categorizerTrainingService = null,
        IIndexNowService? indexNowService = null,
        IConfiguration? config = null,
        IStoryClusteringService? clusteringService = null,
        IArticleEnrichmentService? enrichmentService = null)
    {
        _db                         = db;
        _relatedService             = relatedService;
        _searchService              = searchService;
        _cache                      = cache;
        _logger                     = logger;
        _slugService                = slugService;
        _seoBackfillService         = seoBackfillService;
        _categorizerTrainingService = categorizerTrainingService;
        _indexNowService            = indexNowService;
        _clusteringService          = clusteringService;
        _enrichmentService          = enrichmentService;
        _config                     = config;
    }

    private bool IsAuthorizedInternal()
    {
        if (_config == null) return false;
        var configuredKey = _config["ApiKey"] ?? _config["ApiSecurity:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey)) return false;

        if (Request.Headers.TryGetValue("X-Api-Key", out var providedKey) &&
            !string.IsNullOrEmpty(providedKey))
        {
            var providedBytes = System.Text.Encoding.UTF8.GetBytes(providedKey.ToString());
            var configuredBytes = System.Text.Encoding.UTF8.GetBytes(configuredKey);
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
        }

        return false;
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

        var isInternal = IsAuthorizedInternal();
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
                            Id          = a.Id,
                            Title       = a.Title,
                            Summary     = a.Summary,
                            Content     = isInternal ? a.Content : null,
                            Url         = a.Url,
                            ImageUrl    = CategoryImageMap.Resolve(a.ImageUrl, a.Category), // Fix #23
                            Source      = a.Source ?? "General News",
                            Category    = a.Category ?? "General",
                            PublishedAt = a.PublishedAt,
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
                            Slug        = a.Slug,
                            IsSponsored = a.IsSponsored && (a.CampaignExpiresAt == null || a.CampaignExpiresAt > DateTime.UtcNow),
                            SponsorName = a.SponsorName,
                            SponsorUrl  = a.SponsorUrl,
                            IsPinned    = a.IsPinned,
                            TargetPosition = a.TargetPosition,
                            PriorityWeight = a.PriorityWeight,
                            HasAiSummary = enr != null,
                            AiSummary    = aiSummary,
                            AiSummaryEngine = enr?.Engine,
                            KeyTakeaways = enr?.KeyTakeaways ?? new List<string>(),
                            CommentCount = commentCounts.GetValueOrDefault(a.Id, 0)
                        };
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
        [FromQuery] string? source = null,
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] string? contentType = null,
        [FromQuery] bool    todayOnly  = false,
        [FromQuery] int?    days       = null,
        [FromQuery] bool?   hasAiSummary = null,
        [FromQuery] int     limit      = 200,
        [FromQuery] int     page       = 1,
        [FromQuery] int     pageSize   = 50,
        CancellationToken   cancellationToken = default)
    {
        var query = _db.Articles.AsNoTracking();

        if (hasAiSummary.HasValue)
        {
            if (hasAiSummary.Value)
            {
                query = query.Where(a => _db.ArticleEnrichments.Any(e => e.ArticleId == a.Id));
            }
            else
            {
                query = query.Where(a => !_db.ArticleEnrichments.Any(e => e.ArticleId == a.Id));
            }
        }

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

        var commentCounts = pageIds.Count > 0
            ? await _db.StoryComments.AsNoTracking()
                .Where(c => pageIds.Contains(c.ArticleId) && c.Status != "Hidden")
                .GroupBy(c => c.ArticleId)
                .Select(g => new { ArticleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ArticleId, x => x.Count, cancellationToken)
            : new Dictionary<string, int>();

        var enrichmentMap = pageIds.Count > 0
            ? await _db.ArticleEnrichments.AsNoTracking()
                .Where(e => pageIds.Contains(e.ArticleId))
                .ToDictionaryAsync(e => e.ArticleId, cancellationToken)
            : new Dictionary<string, ArticleEnrichment>();

        var isInternal = IsAuthorizedInternal();
        var articleDtos = rawArticles.Select(a =>
        {
            var enr = enrichmentMap.GetValueOrDefault(a.Id);
            return new ArticleDto
            {
                Id          = a.Id,
                Title       = a.Title,
                Slug        = !string.IsNullOrEmpty(a.Slug) ? a.Slug : _slugService.GenerateArticleSlug(a.Title, a.Id),
                Excerpt     = a.Excerpt ?? _slugService.GenerateExcerpt(a.Content, a.Summary, 300),
                SeoTitle    = a.SeoTitle,
                SeoDescription = a.SeoDescription,
                Summary     = a.Summary,
                Content     = isInternal ? a.Content : null,
                Url         = a.Url,
                ImageUrl    = CategoryImageMap.Resolve(a.ImageUrl, a.Category), // Fix #23
                Source      = string.IsNullOrWhiteSpace(a.Source)   ? "General News" : a.Source,
                Category    = string.IsNullOrWhiteSpace(a.Category) ? "General"      : a.Category,
                PublishedAt = a.PublishedAt,
                AudioUrl    = audioMap.GetValueOrDefault(a.Id) ?? a.AudioUrl,
                Author      = a.Author,
                ContentType = string.IsNullOrWhiteSpace(a.ContentType) ? "News" : a.ContentType,
                CommentCount = commentCounts.GetValueOrDefault(a.Id, 0),
                IsSponsored = a.IsSponsored,
                SponsorName = a.SponsorName,
                SponsorUrl  = a.SponsorUrl,
                CampaignExpiresAt = a.CampaignExpiresAt,
                IsPinned    = a.IsPinned,
                ImpressionCount = a.ImpressionCount,
                ClickCount  = a.ClickCount,
                HasAiSummary = enr != null,
                AiSummaryEngine = enr?.Engine,
                AiSummary   = enr?.FullSummary?.ExecutiveOverview ?? enr?.KeyTakeaways.FirstOrDefault(),
                Enrichment  = enr != null ? new ArticleEnrichmentDto
                {
                    KeyTakeaways = enr.KeyTakeaways,
                    TimelineFacts = enr.TimelineFacts,
                    Faqs = enr.Faqs,
                    FullSummary = enr.FullSummary != null ? new FullSummaryDto
                    {
                        ExecutiveOverview = enr.FullSummary.ExecutiveOverview,
                        ContextAndBackground = enr.FullSummary.ContextAndBackground,
                        KeyFiguresAndEntities = enr.FullSummary.KeyFiguresAndEntities,
                        OutlookOrNextSteps = enr.FullSummary.OutlookOrNextSteps,
                        ReadingTimeSeconds = enr.FullSummary.ReadingTimeSeconds
                    } : null,
                    Engine = enr.Engine,
                    UpdatedAt = enr.UpdatedAt ?? enr.CreatedAt
                } : null
            };
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

        var commentCount = await _db.StoryComments.AsNoTracking()
            .CountAsync(c => c.ArticleId == a.Id && c.Status != "Hidden", cancellationToken);

        var enr = await _db.ArticleEnrichments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ArticleId == a.Id, cancellationToken);

        var dto = new ArticleDto
        {
            Id          = a.Id,
            Title       = a.Title,
            Slug        = !string.IsNullOrEmpty(a.Slug) ? a.Slug : _slugService.GenerateArticleSlug(a.Title, a.Id),
            Excerpt     = a.Excerpt ?? _slugService.GenerateExcerpt(a.Content, a.Summary, 300),
            SeoTitle    = a.SeoTitle,
            SeoDescription = a.SeoDescription,
            Summary     = a.Summary,
            Content     = a.Content,
            Url         = a.Url,
            ImageUrl    = CategoryImageMap.Resolve(a.ImageUrl, a.Category),
            Source      = string.IsNullOrWhiteSpace(a.Source)   ? "General News" : a.Source,
            Category    = string.IsNullOrWhiteSpace(a.Category) ? "General"      : a.Category,
            PublishedAt = a.PublishedAt,
            AudioUrl    = audio ?? a.AudioUrl,
            Author      = a.Author,
            ContentType = string.IsNullOrWhiteSpace(a.ContentType) ? "News" : a.ContentType,
            CommentCount = commentCount,
            IsSponsored = a.IsSponsored,
            SponsorName = a.SponsorName,
            SponsorUrl  = a.SponsorUrl,
            CampaignExpiresAt = a.CampaignExpiresAt,
            IsPinned    = a.IsPinned,
            ImpressionCount = a.ImpressionCount,
            ClickCount  = a.ClickCount,
            HasAiSummary = enr != null,
            AiSummaryEngine = enr?.Engine,
            AiSummary   = enr?.FullSummary?.ExecutiveOverview ?? enr?.KeyTakeaways.FirstOrDefault(),
            Enrichment  = enr != null ? new ArticleEnrichmentDto
            {
                KeyTakeaways = enr.KeyTakeaways,
                TimelineFacts = enr.TimelineFacts,
                Faqs = enr.Faqs,
                FullSummary = enr.FullSummary != null ? new FullSummaryDto
                {
                    ExecutiveOverview = enr.FullSummary.ExecutiveOverview,
                    ContextAndBackground = enr.FullSummary.ContextAndBackground,
                    KeyFiguresAndEntities = enr.FullSummary.KeyFiguresAndEntities,
                    OutlookOrNextSteps = enr.FullSummary.OutlookOrNextSteps,
                    ReadingTimeSeconds = enr.FullSummary.ReadingTimeSeconds
                } : null,
                Engine = enr.Engine,
                UpdatedAt = enr.UpdatedAt ?? enr.CreatedAt
            } : null
        };

        return Ok(dto);
    }

    /// <summary>
    /// Public SEO endpoint for retrieving a single article by canonical slug.
    /// Never returns full raw scraped content to adhere to aggregator copyright compliance.
    /// Sets Cache-Control: public, max-age=60 with ETag validation.
    /// </summary>
    [HttpGet("by-slug/{slug}")]
    [ProducesResponseType(typeof(ArticleDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload
                {
                    Code = "NOT_FOUND",
                    Message = "Article slug is required.",
                    TraceId = HttpContext.TraceIdentifier
                }
            });
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();

        // 1. Exact match on slug
        var article = await _db.Articles
            .AsNoTracking()
            .Include(a => a.ArticleTopics)
                .ThenInclude(at => at.Topic)
            .Include(a => a.Enrichment)
            .FirstOrDefaultAsync(a => a.Slug == normalizedSlug, cancellationToken);

        string? redirectSlug = null;

        // 2. Fallback prefix/suffix match if slug has a unique 6-char hash
        if (article == null)
        {
            var parts = normalizedSlug.Split('-');
            if (parts.Length > 1 && parts[^1].Length >= 4)
            {
                var suffix = parts[^1];
                article = await _db.Articles
                    .AsNoTracking()
                    .Include(a => a.ArticleTopics)
                        .ThenInclude(at => at.Topic)
                    .Include(a => a.Enrichment)
                    .FirstOrDefaultAsync(a => a.Slug.EndsWith(suffix) || a.Id.StartsWith(suffix), cancellationToken);

                if (article != null && !string.Equals(article.Slug, normalizedSlug, StringComparison.OrdinalIgnoreCase))
                {
                    redirectSlug = article.Slug;
                }
            }
        }

        // 3. Fallback match by exact article ID (e.g. if shared using article ID)
        if (article == null)
        {
            article = await _db.Articles
                .AsNoTracking()
                .Include(a => a.ArticleTopics)
                    .ThenInclude(at => at.Topic)
                .Include(a => a.Enrichment)
                .FirstOrDefaultAsync(a => a.Id == slug.Trim() || a.Id.ToLower() == normalizedSlug, cancellationToken);

            if (article != null && !string.IsNullOrWhiteSpace(article.Slug))
            {
                redirectSlug = article.Slug;
            }
        }

        if (article == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload
                {
                    Code = "NOT_FOUND",
                    Message = $"No article found matching slug '{slug}'.",
                    TraceId = HttpContext.TraceIdentifier
                }
            });
        }

        // Generate ETag from article metadata
        var lastModified = article.UpdatedAt ?? article.PublishedAt ?? DateTime.UtcNow;
        var eTag = $"\"{article.Id}_{lastModified.Ticks}_{article.Slug}\"";

        if (Request.Headers.TryGetValue("If-None-Match", out var clientETag) && clientETag == eTag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.Append("ETag", eTag);
        Response.Headers.Append("Cache-Control", "public, max-age=60");

        // Load up to 6 related stories using vector embedding similarity (or topic/category fallback)
        List<ArticleDto> relatedArticles;
        if (_clusteringService != null)
        {
            var relatedEntities = await _clusteringService.FindRelatedArticlesAsync(article.Id, 6, cancellationToken);
            relatedArticles = relatedEntities.Select(a => new ArticleDto
            {
                Id = a.Id,
                Title = a.Title,
                Slug = !string.IsNullOrEmpty(a.Slug) ? a.Slug : _slugService.GenerateArticleSlug(a.Title, a.Id),
                Excerpt = a.Excerpt ?? _slugService.GenerateExcerpt(a.Content, a.Summary, 300),
                Summary = a.Summary,
                Url = a.Url,
                ImageUrl = CategoryImageMap.Resolve(a.ImageUrl, a.Category),
                Source = a.Source ?? "News Stand NG",
                Category = a.Category ?? "General",
                PublishedAt = a.PublishedAt,
                AudioUrl = a.AudioUrl,
                Author = a.Author,
                ContentType = a.ContentType,
                IsSponsored = a.IsSponsored,
                SponsorName = a.SponsorName,
                SponsorUrl = a.SponsorUrl
            }).ToList();
        }
        else
        {
            var relatedQuery = _db.Articles
                .AsNoTracking()
                .Where(a => a.Id != article.Id && a.Category == article.Category)
                .OrderByDescending(a => a.PublishedAt)
                .Take(6);

            relatedArticles = await relatedQuery
                .Select(a => new ArticleDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    Slug = !string.IsNullOrEmpty(a.Slug) ? a.Slug : _slugService.GenerateArticleSlug(a.Title, a.Id),
                    Excerpt = a.Excerpt ?? _slugService.GenerateExcerpt(a.Content, a.Summary, 300),
                    Summary = a.Summary,
                    Url = a.Url,
                    ImageUrl = CategoryImageMap.Resolve(a.ImageUrl, a.Category),
                    Source = a.Source ?? "News Stand NG",
                    Category = a.Category ?? "General",
                    PublishedAt = a.PublishedAt,
                    AudioUrl = a.AudioUrl,
                    Author = a.Author,
                    ContentType = a.ContentType,
                    IsSponsored = a.IsSponsored,
                    SponsorName = a.SponsorName,
                    SponsorUrl = a.SponsorUrl
                })
                .ToListAsync(cancellationToken);
        }

        var detailDto = new ArticleDetailDto
        {
            Id = article.Id,
            Title = article.Title,
            Slug = article.Slug,
            Excerpt = article.Excerpt ?? _slugService.GenerateExcerpt(article.Content, article.Summary, 300),
            Source = article.Source,
            SourceUrl = article.Url,
            ImageUrl = CategoryImageMap.Resolve(article.ImageUrl, article.Category),
            PublishedAt = article.PublishedAt,
            UpdatedAt = article.UpdatedAt,
            Author = article.Author,
            Category = article.Category,
            ContentType = article.ContentType,
            AudioUrl = article.AudioUrl,
            SeoTitle = article.SeoTitle,
            SeoDescription = article.SeoDescription,
            IsSponsored = article.IsSponsored,
            SponsorName = article.SponsorName,
            SponsorUrl = article.SponsorUrl,
            RedirectSlug = redirectSlug,
            Topics = article.ArticleTopics
                .Where(at => at.Topic != null)
                .Select(at => new TopicDto
                {
                    Id = at.Topic!.Id,
                    Slug = at.Topic.Slug,
                    Name = at.Topic.Name,
                    Type = at.Topic.Type.ToString(),
                    Description = at.Topic.Description,
                    CreatedAt = at.Topic.CreatedAt
                })
                .ToList(),
            RelatedArticles = relatedArticles,
            Enrichment = article.Enrichment != null
                ? new ArticleEnrichmentDto
                {
                    KeyTakeaways = article.Enrichment.KeyTakeaways,
                    TimelineFacts = article.Enrichment.TimelineFacts,
                    Faqs = article.Enrichment.Faqs,
                    FullSummary = article.Enrichment.FullSummary != null
                        ? new FullSummaryDto
                        {
                            ExecutiveOverview = article.Enrichment.FullSummary.ExecutiveOverview,
                            ContextAndBackground = article.Enrichment.FullSummary.ContextAndBackground,
                            KeyFiguresAndEntities = article.Enrichment.FullSummary.KeyFiguresAndEntities,
                            OutlookOrNextSteps = article.Enrichment.FullSummary.OutlookOrNextSteps,
                            ReadingTimeSeconds = article.Enrichment.FullSummary.ReadingTimeSeconds
                        }
                        : null,
                    Engine = article.Enrichment.Engine,
                    UpdatedAt = article.Enrichment.UpdatedAt ?? article.Enrichment.CreatedAt
                }
                : null
        };

        return Ok(detailDto);
    }

    /// <summary>
    /// Returns or generates the AI Executive Briefing summary of the full news article body.
    /// Can be requested by article ID or article slug.
    /// </summary>
    [HttpGet("{id}/ai-summary")]
    [HttpPost("{id}/ai-summary")]
    [ProducesResponseType(typeof(FullSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrGenerateAiSummary(string id, CancellationToken cancellationToken = default)
    {
        if (_enrichmentService == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponseDto
            {
                Error = new ErrorPayload
                {
                    Code = "SERVICE_UNAVAILABLE",
                    Message = "Article enrichment service is not registered.",
                    TraceId = HttpContext.TraceIdentifier
                }
            });
        }

        var article = await _db.Articles
            .Include(a => a.Enrichment)
            .FirstOrDefaultAsync(a => a.Id == id || a.Slug == id, cancellationToken);

        if (article == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload
                {
                    Code = "NOT_FOUND",
                    Message = $"Article '{id}' was not found.",
                    TraceId = HttpContext.TraceIdentifier
                }
            });
        }

        if (article.Enrichment?.FullSummary != null)
        {
            var cached = article.Enrichment.FullSummary;
            return Ok(new FullSummaryDto
            {
                ExecutiveOverview = cached.ExecutiveOverview,
                ContextAndBackground = cached.ContextAndBackground,
                KeyFiguresAndEntities = cached.KeyFiguresAndEntities,
                OutlookOrNextSteps = cached.OutlookOrNextSteps,
                ReadingTimeSeconds = cached.ReadingTimeSeconds
            });
        }

        var enriched = await _enrichmentService.EnrichArticleAsync(article.Id, cancellationToken);
        if (enriched?.FullSummary != null)
        {
            var generated = enriched.FullSummary;
            return Ok(new FullSummaryDto
            {
                ExecutiveOverview = generated.ExecutiveOverview,
                ContextAndBackground = generated.ContextAndBackground,
                KeyFiguresAndEntities = generated.KeyFiguresAndEntities,
                OutlookOrNextSteps = generated.OutlookOrNextSteps,
                ReadingTimeSeconds = generated.ReadingTimeSeconds
            });
        }

        return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseDto
        {
            Error = new ErrorPayload
            {
                Code = "GENERATION_FAILED",
                Message = "Failed to generate AI executive summary.",
                TraceId = HttpContext.TraceIdentifier
            }
        });
    }

    /// <summary>
    /// Enriches an article by ID with 3-bullet Key Takeaways, Timeline Facts, and FAQ pairs.
    /// Internal / administrative operation.
    /// </summary>
    [HttpPost("{id}/enrich")]
    [ProducesResponseType(typeof(ArticleEnrichmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnrichArticle(string id, CancellationToken cancellationToken = default)
    {
        if (_enrichmentService == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponseDto
            {
                Error = new ErrorPayload
                {
                    Code = "SERVICE_UNAVAILABLE",
                    Message = "Article enrichment service is not registered.",
                    TraceId = HttpContext.TraceIdentifier
                }
            });
        }

        var result = await _enrichmentService.EnrichArticleAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload
                {
                    Code = "NOT_FOUND",
                    Message = $"Article with id '{id}' was not found.",
                    TraceId = HttpContext.TraceIdentifier
                }
            });
        }

        return Ok(new ArticleEnrichmentDto
        {
            KeyTakeaways = result.KeyTakeaways,
            TimelineFacts = result.TimelineFacts,
            Faqs = result.Faqs,
            FullSummary = result.FullSummary != null
                ? new FullSummaryDto
                {
                    ExecutiveOverview = result.FullSummary.ExecutiveOverview,
                    ContextAndBackground = result.FullSummary.ContextAndBackground,
                    KeyFiguresAndEntities = result.FullSummary.KeyFiguresAndEntities,
                    OutlookOrNextSteps = result.FullSummary.OutlookOrNextSteps,
                    ReadingTimeSeconds = result.FullSummary.ReadingTimeSeconds
                }
                : null,
            Engine = result.Engine,
            UpdatedAt = result.UpdatedAt ?? result.CreatedAt
        });
    }

    /// <summary>
    /// Gets the existing AI/ML enrichment data for an article by ID.
    /// </summary>
    [HttpGet("{id}/enrichment")]
    [ProducesResponseType(typeof(ArticleEnrichmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArticleEnrichment(string id, CancellationToken cancellationToken = default)
    {
        var enrichment = await _db.ArticleEnrichments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ArticleId == id, cancellationToken);

        if (enrichment == null)
        {
            var art = await _db.Articles.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Slug == id || a.Id == id, cancellationToken);
            if (art != null)
            {
                enrichment = await _db.ArticleEnrichments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.ArticleId == art.Id, cancellationToken);
            }
        }

        if (enrichment == null)
        {
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorPayload
                {
                    Code = "NOT_FOUND",
                    Message = $"No enrichment found for article '{id}'.",
                    TraceId = HttpContext.TraceIdentifier
                }
            });
        }

        return Ok(new ArticleEnrichmentDto
        {
            KeyTakeaways = enrichment.KeyTakeaways,
            TimelineFacts = enrichment.TimelineFacts,
            Faqs = enrichment.Faqs,
            FullSummary = enrichment.FullSummary != null
                ? new FullSummaryDto
                {
                    ExecutiveOverview = enrichment.FullSummary.ExecutiveOverview,
                    ContextAndBackground = enrichment.FullSummary.ContextAndBackground,
                    KeyFiguresAndEntities = enrichment.FullSummary.KeyFiguresAndEntities,
                    OutlookOrNextSteps = enrichment.FullSummary.OutlookOrNextSteps,
                    ReadingTimeSeconds = enrichment.FullSummary.ReadingTimeSeconds
                }
                : null,
            Engine = enrichment.Engine,
            UpdatedAt = enrichment.UpdatedAt ?? enrichment.CreatedAt
        });
    }

    /// <summary>
    /// Runs a batch cycle enriching up to limit unenriched articles.
    /// </summary>
    [HttpPost("enrich-batch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessEnrichmentBatch([FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        if (_enrichmentService == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Enrichment service not registered.");
        }

        int processed = await _enrichmentService.ProcessUnenrichedBatchAsync(limit, cancellationToken);
        return Ok(new { Processed = processed, Timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Serves a rich preview HTML page containing Twitter Card (summary_large_image) and Open Graph metadata
    /// for social sharing on 𝕏 (Twitter), WhatsApp, and other platforms, with image preview, deep-linking, and app store CTA.
    /// Accessible via:
    ///   - GET /api/v1/articles/share/{id}
    ///   - GET /share/{id}
    /// </summary>
    [HttpGet("share/{id}")]
    [HttpGet("/share/{id}")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetArticleShareCard(
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

        var imageUrl = CategoryImageMap.Resolve(a.ImageUrl, a.Category);
        var title = System.Net.WebUtility.HtmlEncode(a.Title ?? "News Stand NG Story");
        var source = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(a.Source) ? "News Stand NG" : a.Source);
        var summary = System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(a.Summary) ? $"Read the latest breaking story from {source} on News Stand NG." : a.Summary);
        var targetUrl = a.Url ?? "https://play.google.com/store/apps/details?id=com.nigeriannewsgrid.app";
        var appStoreUrl = "https://play.google.com/store/apps/details?id=com.nigeriannewsgrid.app";
        var deepLinkUri = $"nigeriannewsgrid://article/{id}";

        var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>{title} — News Stand NG</title>

    <!-- 𝕏 (Twitter) Card Metadata -->
    <meta name=""twitter:card"" content=""summary_large_image"">
    <meta name=""twitter:site"" content=""@NewsStandNG"">
    <meta name=""twitter:creator"" content=""@NewsStandNG"">
    <meta name=""twitter:title"" content=""{title}"">
    <meta name=""twitter:description"" content=""Via {source} • {summary}"">
    <meta name=""twitter:image"" content=""{imageUrl}"">
    <meta name=""twitter:image:alt"" content=""{title}"">

    <!-- Open Graph Metadata -->
    <meta property=""og:type"" content=""article"">
    <meta property=""og:site_name"" content=""News Stand NG"">
    <meta property=""og:title"" content=""{title}"">
    <meta property=""og:description"" content=""Via {source} • {summary}"">
    <meta property=""og:image"" content=""{imageUrl}"">
    <meta property=""og:url"" content=""{targetUrl}"">

    <style>
        :root {{
            --bg: #F8FAFC;
            --card-bg: #FFFFFF;
            --text-primary: #0F172A;
            --text-secondary: #64748B;
            --accent: #2563EB;
            --accent-hover: #1D4ED8;
            --border: #E2E8F0;
        }}
        @media (prefers-color-scheme: dark) {{
            :root {{
                --bg: #090D16;
                --card-bg: #131B2E;
                --text-primary: #F8FAFC;
                --text-secondary: #94A3B8;
                --border: #1E293B;
            }}
        }}
        * {{ box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }}
        body {{ background-color: var(--bg); color: var(--text-primary); display: flex; justify-content: center; align-items: center; min-height: 100vh; padding: 20px; }}
        .card {{ background: var(--card-bg); border: 1px solid var(--border); border-radius: 20px; max-width: 600px; width: 100%; overflow: hidden; box-shadow: 0 10px 25px -5px rgba(0,0,0,0.1); }}
        .image-container {{ position: relative; width: 100%; height: 280px; background: #E2E8F0; }}
        .image-container img {{ width: 100%; height: 100%; object-fit: cover; }}
        .source-badge {{ position: absolute; top: 16px; left: 16px; background: rgba(15, 23, 42, 0.85); color: #FFF; padding: 6px 12px; border-radius: 999px; font-size: 12px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; backdrop-filter: blur(8px); }}
        .content {{ padding: 24px; }}
        .headline {{ font-size: 20px; line-height: 1.4; font-weight: 700; margin-bottom: 12px; }}
        .summary {{ font-size: 14px; line-height: 1.6; color: var(--text-secondary); margin-bottom: 24px; }}
        .actions {{ display: flex; flex-direction: column; gap: 12px; }}
        .btn {{ display: flex; align-items: center; justify-content: center; gap: 8px; padding: 14px 20px; border-radius: 12px; font-size: 15px; font-weight: 600; text-decoration: none; transition: transform 0.15s ease, background 0.15s ease; cursor: pointer; text-align: center; }}
        .btn:active {{ transform: scale(0.98); }}
        .btn-primary {{ background: var(--accent); color: #FFF; }}
        .btn-primary:hover {{ background: var(--accent-hover); }}
        .btn-secondary {{ background: transparent; border: 1.5px solid var(--border); color: var(--text-primary); }}
        .footer-note {{ text-align: center; margin-top: 18px; font-size: 12px; color: var(--text-secondary); }}
    </style>
</head>
<body>
    <div class=""card"">
        <div class=""image-container"">
            <img src=""{imageUrl}"" alt=""{title}"" onerror=""this.style.display='none'"">
            <span class=""source-badge"">{source}</span>
        </div>
        <div class=""content"">
            <h1 class=""headline"">{title}</h1>
            <p class=""summary"">{summary}</p>
            <div class=""actions"">
                <a href=""{deepLinkUri}"" class=""btn btn-primary"">📲 Open in News Stand NG</a>
                <a href=""{appStoreUrl}"" class=""btn btn-secondary"">⭐ Download App on Google Play</a>
                <a href=""{targetUrl}"" target=""_blank"" rel=""noopener noreferrer"" class=""btn btn-secondary"">🌐 Read Full Story at {source}</a>
            </div>
            <p class=""footer-note"">Powered by News Stand NG — Nigeria's Premier Smart News Aggregator</p>
        </div>
    </div>
</body>
</html>";

        return Content(html, "text/html; charset=utf-8");
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
        var ingestedSlugs = new List<string>();

        foreach (var item in incomingList)
        {
            var titleNorm = item.Title.Trim().ToLowerInvariant();
            var urlNorm   = item.Url?.Trim();

            bool isDuplicate = (!string.IsNullOrEmpty(urlNorm) && existingUrls.Contains(urlNorm)) ||
                               existingTitles.Contains(titleNorm);

            if (isDuplicate) { skipped++; continue; }

            var articleId = Guid.NewGuid().ToString("N");
            var title = item.Title.Trim();
            var slug = _slugService.GenerateArticleSlug(title, articleId);
            var excerpt = _slugService.GenerateExcerpt(item.Content, item.Summary, 300);

            _db.Articles.Add(new Article
            {
                Id          = articleId,
                Title       = title,
                Slug        = slug,
                Excerpt     = excerpt,
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

            if (item.Topics != null && item.Topics.Count > 0)
            {
                foreach (var t in item.Topics)
                {
                    if (string.IsNullOrWhiteSpace(t.Slug)) continue;
                    var topicSlug = t.Slug.Trim().ToLowerInvariant();

                    var existingTopic = await _db.Topics.FirstOrDefaultAsync(x => x.Slug == topicSlug, cancellationToken);
                    if (existingTopic == null)
                    {
                        Enum.TryParse<TopicType>(t.Type, true, out var parsedType);
                        existingTopic = new Topic
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Name = t.Name,
                            Slug = topicSlug,
                            Type = parsedType,
                            Description = t.Description,
                            CreatedAt = DateTime.UtcNow
                        };
                        _db.Topics.Add(existingTopic);
                    }

                    _db.ArticleTopics.Add(new ArticleTopic
                    {
                        ArticleId = articleId,
                        TopicId = existingTopic.Id,
                        Confidence = t.Confidence
                    });
                }
            }

            if (!string.IsNullOrEmpty(urlNorm)) existingUrls.Add(urlNorm);
            existingTitles.Add(titleNorm);
            ingestedSlugs.Add(slug);
            ingested++;
        }

        if (ingested > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            InvalidateBriefingCache(); // Fix #31

            if (_indexNowService != null && ingestedSlugs.Count > 0)
            {
                var host = _indexNowService.GetConfiguration().Host;
                var urls = ingestedSlugs.Select(s => $"https://{host}/news/{s}");
                _ = _indexNowService.EnqueueUrlsAsync(urls, CancellationToken.None);
            }
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
                PublishedAt = a.PublishedAt,
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

        var articleId = Guid.NewGuid().ToString("N");
        var title = request.Title.Trim();
        var slug = _slugService.GenerateArticleSlug(title, articleId);
        var excerpt = _slugService.GenerateExcerpt(request.Content, request.Summary, 300);

        var article = new Article
        {
            Id                = articleId,
            Title             = title,
            Slug              = slug,
            Excerpt           = excerpt,
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

    /// <summary>
    /// Admin endpoint for idempotent backfilling of SEO slugs and excerpts.
    /// Protected by ApiKey middleware.
    /// </summary>
    [HttpPost("seo/backfill")]
    public async Task<IActionResult> BackfillSeo(
        [FromQuery] int batchSize = 500,
        CancellationToken cancellationToken = default)
    {
        if (_seoBackfillService == null)
        {
            return StatusCode(500, new { message = "ArticleSeoBackfillService is not registered." });
        }

        var result = await _seoBackfillService.BackfillArticleSeoAsync(batchSize, cancellationToken);
        return Ok(result);
    }
}

public class UpdateArticleCategoryRequest
{
    public string Category { get; set; } = string.Empty;
}
