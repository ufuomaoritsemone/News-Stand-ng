using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;

using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Route("api/[controller]")]
public class SeoController(
    NewsDbContext db,
    ILogger<SeoController> logger,
    ISearchConsoleService? searchConsoleService = null,
    SeoMonitoringBackgroundService? monitoringService = null) : ControllerBase
{
    private readonly ISearchConsoleService? _searchConsole = searchConsoleService;
    private readonly SeoMonitoringBackgroundService? _monitoringService = monitoringService;
    private static readonly string[] StandardCategories =
    [
        "politics", "business", "technology", "entertainment", "sports", "metro", "opinion"
    ];

    /// <summary>
    /// Returns standardized sitemap URLs and metadata for sitemap generators and crawlers.
    /// Supports types: news (rolling 48h, non-sponsored), articles, topics, categories, sources.
    /// Sets Cache-Control: public, max-age=60 with ETag support.
    /// </summary>
    [HttpGet("sitemap-entries")]
    [ProducesResponseType(typeof(SitemapEntriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> GetSitemapEntries(
        [FromQuery] string type = "news",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 1000,
        CancellationToken ct = default)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 10, 50000);
        var normalizedType = type?.Trim().ToLowerInvariant() ?? "news";
        logger.LogDebug("Serving sitemap entries: type={Type}, page={Page}, pageSize={PageSize}", normalizedType, safePage, safePageSize);

        var response = new SitemapEntriesResponse
        {
            Type = normalizedType,
            Page = safePage,
            PageSize = safePageSize
        };

        switch (normalizedType)
        {
            case "news":
            {
                // Google News sitemap standard: articles published within the last 48 hours, non-sponsored only
                var cutoff = DateTime.UtcNow.AddHours(-48);
                var query = db.Articles
                    .AsNoTracking()
                    .Where(a => !a.IsSponsored && a.PublishedAt >= cutoff && !string.IsNullOrEmpty(a.Slug))
                    .OrderByDescending(a => a.PublishedAt);

                response.TotalCount = await query.CountAsync(ct);

                var items = await query
                    .Skip((safePage - 1) * safePageSize)
                    .Take(safePageSize)
                    .Select(a => new
                    {
                        a.Slug,
                        a.Title,
                        a.PublishedAt,
                        a.UpdatedAt
                    })
                    .ToListAsync(ct);

                response.Entries = items.Select(a => new SitemapEntryDto
                {
                    Loc = $"/news/{a.Slug}",
                    LastMod = a.UpdatedAt ?? a.PublishedAt,
                    Title = a.Title,
                    PublishedAt = a.PublishedAt,
                    PublicationName = "News Stand NG",
                    PublicationLanguage = "en",
                    ChangeFreq = "hourly",
                    Priority = 0.9
                }).ToList();
                break;
            }

            case "articles":
            {
                var query = db.Articles
                    .AsNoTracking()
                    .Where(a => !string.IsNullOrEmpty(a.Slug))
                    .OrderByDescending(a => a.PublishedAt);

                response.TotalCount = await query.CountAsync(ct);

                var items = await query
                    .Skip((safePage - 1) * safePageSize)
                    .Take(safePageSize)
                    .Select(a => new { a.Slug, a.PublishedAt, a.UpdatedAt })
                    .ToListAsync(ct);

                response.Entries = items.Select(a => new SitemapEntryDto
                {
                    Loc = $"/news/{a.Slug}",
                    LastMod = a.UpdatedAt ?? a.PublishedAt,
                    ChangeFreq = "daily",
                    Priority = 0.7
                }).ToList();
                break;
            }

            case "topics":
            {
                var query = db.Topics
                    .AsNoTracking()
                    .OrderBy(t => t.Slug);

                response.TotalCount = await query.CountAsync(ct);

                var items = await query
                    .Skip((safePage - 1) * safePageSize)
                    .Take(safePageSize)
                    .Select(t => new { t.Slug, t.CreatedAt })
                    .ToListAsync(ct);

                response.Entries = items.Select(t => new SitemapEntryDto
                {
                    Loc = $"/topic/{t.Slug}",
                    LastMod = t.CreatedAt,
                    ChangeFreq = "daily",
                    Priority = 0.8
                }).ToList();
                break;
            }

            case "categories":
            {
                response.TotalCount = StandardCategories.Length;
                response.Entries = StandardCategories.Select(c => new SitemapEntryDto
                {
                    Loc = $"/category/{c}",
                    LastMod = DateTime.UtcNow.Date,
                    ChangeFreq = "hourly",
                    Priority = 0.8
                }).ToList();
                break;
            }

            case "sources":
            {
                var query = db.Sources
                    .AsNoTracking()
                    .Where(s => !string.IsNullOrEmpty(s.Slug))
                    .OrderBy(s => s.Slug);

                response.TotalCount = await query.CountAsync(ct);

                var items = await query
                    .Skip((safePage - 1) * safePageSize)
                    .Take(safePageSize)
                    .Select(s => new { s.Slug })
                    .ToListAsync(ct);

                response.Entries = items.Select(s => new SitemapEntryDto
                {
                    Loc = $"/source/{s.Slug}",
                    LastMod = DateTime.UtcNow.Date,
                    ChangeFreq = "weekly",
                    Priority = 0.6
                }).ToList();
                break;
            }

            case "stories":
            {
                var query = db.StoryClusters
                    .AsNoTracking()
                    .Where(sc => sc.ArticleCount >= 2 && sc.SourceCount >= 2 && !string.IsNullOrEmpty(sc.Slug))
                    .OrderByDescending(sc => sc.UpdatedAt);

                response.TotalCount = await query.CountAsync(ct);

                var items = await query
                    .Skip((safePage - 1) * safePageSize)
                    .Take(safePageSize)
                    .Select(sc => new { sc.Slug, sc.UpdatedAt, sc.Title })
                    .ToListAsync(ct);

                response.Entries = items.Select(sc => new SitemapEntryDto
                {
                    Loc = $"/story/{sc.Slug}",
                    LastMod = sc.UpdatedAt,
                    Title = sc.Title,
                    ChangeFreq = "hourly",
                    Priority = 0.85
                }).ToList();
                break;
            }

            default:
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = new ErrorPayload
                    {
                        Code = "INVALID_TYPE",
                        Message = $"Unknown sitemap type '{type}'. Allowed values: news, articles, topics, categories, sources, stories.",
                        TraceId = HttpContext.TraceIdentifier
                    }
                });
            }
        }

        var eTag = $"\"sitemap_{normalizedType}_{safePage}_{safePageSize}_{response.TotalCount}\"";
        if (Request.Headers.TryGetValue("If-None-Match", out var clientETag) && clientETag == eTag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.Append("ETag", eTag);
        Response.Headers.Append("Cache-Control", "public, max-age=60");

        return Ok(response);
    }

    /// <summary>
    /// Returns the active IndexNow protocol verification configuration.
    /// </summary>
    [HttpGet("indexnow-key")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetIndexNowKey([FromServices] NewsApi.Services.IIndexNowService? indexNowService)
    {
        if (indexNowService is null)
        {
            return Ok(new
            {
                key = "3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d",
                host = "newsstand.ng",
                keyLocation = "https://newsstand.ng/3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d.txt"
            });
        }

        var (key, host, keyLocation) = indexNowService.GetConfiguration();
        return Ok(new
        {
            key,
            host,
            keyLocation
        });
    }

    /// <summary>
    /// Phase 9: Returns aggregated search engine performance telemetry (Clicks, Impressions, CTR, Position).
    /// </summary>
    [HttpGet("search-analytics")]
    [ProducesResponseType(typeof(SearchPerformanceSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSearchAnalytics(
        [FromQuery] int days = 28,
        [FromQuery] string dimension = "query",
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (_searchConsole is null)
        {
            return Ok(new SearchPerformanceSummaryDto
            {
                Days = days,
                DataEngine = "Unconfigured"
            });
        }

        var result = await _searchConsole.GetSearchAnalyticsAsync(days, dimension, limit, ct);
        return Ok(result);
    }

    /// <summary>
    /// Phase 9: Returns comprehensive crawlability and indexation diagnostics across site entities and sitemaps.
    /// </summary>
    [HttpGet("crawl-diagnostics")]
    [ProducesResponseType(typeof(CrawlDiagnosticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCrawlDiagnostics(CancellationToken ct = default)
    {
        if (_searchConsole is null)
        {
            return Ok(new CrawlDiagnosticsDto());
        }

        var result = await _searchConsole.GetCrawlDiagnosticsAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Phase 9: Inspects a single URL returning Google indexing verdict, canonical alignment, mobile friendliness, and schema.
    /// </summary>
    [HttpPost("inspect-url")]
    [ProducesResponseType(typeof(UrlInspectionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InspectUrl(
        [FromBody] UrlInspectionRequestDto request,
        CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorPayload
                {
                    Code = "INVALID_URL",
                    Message = "A valid URL or relative path is required for inspection.",
                    TraceId = HttpContext.TraceIdentifier
                }
            });
        }

        if (_searchConsole is null)
        {
            return Ok(new UrlInspectionResultDto
            {
                Url = request.Url,
                Verdict = "FAIL",
                IndexingState = "Service unconfigured."
            });
        }

        var result = await _searchConsole.InspectUrlAsync(request.Url, ct);
        return Ok(result);
    }

    /// <summary>
    /// Phase 9: Evaluates live health, status, and response latency for all registered XML sitemaps.
    /// </summary>
    [HttpGet("sitemaps/status")]
    [ProducesResponseType(typeof(List<SitemapDiagnosticDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSitemapsStatus(CancellationToken ct = default)
    {
        if (_searchConsole is null)
        {
            return Ok(new List<SitemapDiagnosticDto>());
        }

        var result = await _searchConsole.GetSitemapHealthAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Phase 10: Returns the live multi-factor SEO health scorecard (0–100) and pillar grades.
    /// </summary>
    [HttpGet("health-scorecard")]
    [ProducesResponseType(typeof(SeoHealthScorecardDto), StatusCodes.Status200OK)]
    public IActionResult GetHealthScorecard()
    {
        var scorecard = _monitoringService?.GetLatestScorecard() ?? new SeoHealthScorecardDto();
        return Ok(scorecard);
    }

    /// <summary>
    /// Phase 10: Returns active ML-detected ranking decay and traffic anomaly alerts.
    /// </summary>
    [HttpGet("anomalies")]
    [ProducesResponseType(typeof(List<SeoAnomalyAlertDto>), StatusCodes.Status200OK)]
    public IActionResult GetActiveAnomalies()
    {
        var alerts = _monitoringService?.GetLatestScorecard().ActiveAlerts ?? [];
        return Ok(alerts);
    }

    /// <summary>
    /// Phase 10: Returns high-leverage CTR opportunities with AI-suggested headline alternatives.
    /// </summary>
    [HttpGet("ctr-opportunities")]
    [ProducesResponseType(typeof(List<CtrOpportunityDto>), StatusCodes.Status200OK)]
    public IActionResult GetCtrOpportunities()
    {
        var opps = _monitoringService?.GetLatestScorecard().CtrOpportunities ?? [];
        return Ok(opps);
    }

    /// <summary>
    /// Phase 10: Triggers an automated self-healing remediation action (backfill, IndexNow ping).
    /// </summary>
    [HttpPost("trigger-remediation")]
    [ProducesResponseType(typeof(AutomatedRemediationLogDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerRemediation(
        [FromBody] TriggerRemediationRequestDto request,
        CancellationToken ct = default)
    {
        if (_monitoringService is null)
        {
            return Ok(new AutomatedRemediationLogDto
            {
                ActionType = request?.ActionType ?? "Unknown",
                Status = "Failed",
                Details = "Monitoring background service is not running."
            });
        }

        var log = await _monitoringService.TriggerRemediationAsync(
            request?.ActionType ?? "BackfillEnrichment",
            request?.Target,
            ct);

        return Ok(log);
    }
}
