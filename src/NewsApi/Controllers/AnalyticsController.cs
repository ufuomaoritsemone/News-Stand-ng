using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(NewsDbContext db, ILogger<AnalyticsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Accepts a batch of user behaviour events from the mobile app.
    /// Events are stored as-is; the client flushes its local queue on success.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> TrackEvents(
        [FromBody] List<UserEventDto> events,
        CancellationToken cancellationToken = default)
    {
        if (events is null || events.Count == 0)
            return BadRequest(new { message = "No events provided." });

        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "app_open", "article_read", "article_share", "article_bookmark",
            "audio_listen_start", "audio_listen_complete", "audio_listen", "audio_complete"
        };

        var validEvents = events
            .Where(e => !string.IsNullOrWhiteSpace(e.DeviceId)
                        && !string.IsNullOrWhiteSpace(e.EventType)
                        && allowedTypes.Contains(e.EventType))
            .Select(e => new UserEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                DeviceId = e.DeviceId.Trim()[..Math.Min(e.DeviceId.Trim().Length, 64)],
                EventType = e.EventType.Trim().ToLowerInvariant(),
                ArticleId = e.ArticleId?.Trim(),
                ArticleTitle = e.ArticleTitle?.Trim()[..Math.Min(e.ArticleTitle?.Trim().Length ?? 0, 300)],
                Category = e.Category?.Trim(),
                Platform = e.Platform?.Trim(),
                AppVersion = e.AppVersion?.Trim(),
                OccurredAt = e.OccurredAt == default ? DateTimeOffset.UtcNow : e.OccurredAt,
                ReceivedAt = DateTimeOffset.UtcNow
            })
            .ToList();

        if (validEvents.Count == 0)
            return BadRequest(new { message = "No valid events after validation." });

        _db.UserEvents.AddRange(validEvents);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Analytics: received {Total} events, stored {Valid} valid events from DeviceId(s): {Devices}",
            events.Count, validEvents.Count,
            string.Join(", ", validEvents.Select(e => e.DeviceId[..Math.Min(8, e.DeviceId.Length)] + "…").Distinct()));

        return Ok(new { stored = validEvents.Count });
    }

    /// <summary>
    /// Returns aggregate analytics summary for the admin dashboard.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-Math.Abs(days));
        var allRecent = await _db.UserEvents
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var events = allRecent
            .Where(e => e.OccurredAt >= since)
            .OrderByDescending(e => e.OccurredAt)
            .Take(10000)
            .ToList();

        var totalEvents = events.Count;
        var uniqueDevices = events.Select(e => e.DeviceId).Distinct().Count();

        var byType = events
            .GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => g.Count());

        var topArticles = events
            .Where(e => e.EventType == "article_read" && !string.IsNullOrEmpty(e.ArticleId))
            .GroupBy(e => new { e.ArticleId, e.ArticleTitle, e.Category })
            .Select(g => new TopArticleDto
            {
                ArticleId = g.Key.ArticleId!,
                Title = g.Key.ArticleTitle ?? "(no title)",
                Category = g.Key.Category ?? "General",
                ReadCount = g.Count()
            })
            .OrderByDescending(a => a.ReadCount)
            .Take(10)
            .ToList();

        // Daily breakdown for the last 7 days (for sparkline chart)
        var dailyBreakdown = Enumerable.Range(0, 7)
            .Select(i => DateTimeOffset.UtcNow.Date.AddDays(-i))
            .Select(date => new DailyCountDto
            {
                Date = date.ToString("yyyy-MM-dd"),
                Count = events.Count(e => e.OccurredAt.Date == date)
            })
            .OrderBy(d => d.Date)
            .ToList();

        var audioStarted = byType.GetValueOrDefault("audio_listen_start", 0) + byType.GetValueOrDefault("audio_listen", 0);
        var audioCompleted = byType.GetValueOrDefault("audio_listen_complete", 0) + byType.GetValueOrDefault("audio_complete", 0);
        var audioCompletionRate = audioStarted > 0 ? Math.Round((audioCompleted * 100.0 / audioStarted), 1) : 0.0;

        return Ok(new AnalyticsSummaryDto
        {
            TotalEvents = totalEvents,
            UniqueDevices = uniqueDevices,
            AppOpens = byType.GetValueOrDefault("app_open", 0),
            ArticleReads = byType.GetValueOrDefault("article_read", 0),
            SharesClicked = byType.GetValueOrDefault("article_share", 0),
            BookmarksAdded = byType.GetValueOrDefault("article_bookmark", 0),
            AudioListensStarted = audioStarted,
            AudioListensCompleted = audioCompleted,
            AudioCompletionRate = audioCompletionRate,
            TopArticles = topArticles,
            DailyBreakdown = dailyBreakdown,
            PeriodDays = days
        });
    }

    /// <summary>
    /// Returns paginated raw event log for admin inspection.
    /// </summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? eventType = null,
        CancellationToken cancellationToken = default)
    {
        var size = Math.Clamp(pageSize, 1, 200);
        var pageIndex = Math.Max(1, page);

        var query = _db.UserEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(e => e.EventType == eventType.ToLowerInvariant());

        var allMatching = await query.ToListAsync(cancellationToken);
        var total = allMatching.Count;

        var rawRows = allMatching
            .OrderByDescending(e => e.OccurredAt)
            .Skip((pageIndex - 1) * size)
            .Take(size)
            .ToList();

        var rows = rawRows.Select(e => new UserEventRowDto
        {
            Id = e.Id,
            DeviceId = e.DeviceId.Length > 8 ? e.DeviceId[..8] + "…" : e.DeviceId,
            EventType = e.EventType,
            ArticleId = e.ArticleId,
            ArticleTitle = e.ArticleTitle,
            Category = e.Category,
            Platform = e.Platform,
            AppVersion = e.AppVersion,
            OccurredAt = e.OccurredAt,
            ReceivedAt = e.ReceivedAt
        }).ToList();

        return Ok(new { total, page = pageIndex, pageSize = size, rows });
    }
}

// ──────────────────────────────────────────────────────────
// DTOs
// ──────────────────────────────────────────────────────────

public class UserEventDto
{
    public string DeviceId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? ArticleId { get; set; }
    public string? ArticleTitle { get; set; }
    public string? Category { get; set; }
    public string? Platform { get; set; }
    public string? AppVersion { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public class AnalyticsSummaryDto
{
    public int TotalEvents { get; set; }
    public int UniqueDevices { get; set; }
    public int AppOpens { get; set; }
    public int ArticleReads { get; set; }
    public int SharesClicked { get; set; }
    public int BookmarksAdded { get; set; }
    public int AudioListensStarted { get; set; }
    public int AudioListensCompleted { get; set; }
    public double AudioCompletionRate { get; set; }
    public int PeriodDays { get; set; }
    public List<TopArticleDto> TopArticles { get; set; } = [];
    public List<DailyCountDto> DailyBreakdown { get; set; } = [];
}

public class TopArticleDto
{
    public string ArticleId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int ReadCount { get; set; }
}

public class DailyCountDto
{
    public string Date { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class UserEventRowDto
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? ArticleId { get; set; }
    public string? ArticleTitle { get; set; }
    public string? Category { get; set; }
    public string? Platform { get; set; }
    public string? AppVersion { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}
