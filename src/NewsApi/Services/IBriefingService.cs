using NewsApi.Models;

namespace NewsApi.Services;

public record BriefingResult(
    List<BriefingCategoryDto> Categories,
    bool CacheHit,
    double HoursStale);

public record BriefingAvailabilityResult(
    bool Available,
    int ArticleCount,
    DateTime TimestampUtc);

/// <summary>
/// Assembles categorized daily briefings, checks briefing availability, and manages
/// atomic in-memory cache invalidation via <see cref="CancellationTokenSource"/>.
/// Extracted from ArticlesController (Priority 2.1) to adhere to Single Responsibility Principle.
/// </summary>
public interface IBriefingService
{
    Task<BriefingResult> GetBriefingsAsync(
        int topPerCategory = 5,
        string? language = null,
        bool isInternal = false,
        CancellationToken cancellationToken = default);

    Task<BriefingAvailabilityResult> CheckAvailabilityAsync(
        CancellationToken cancellationToken = default);

    void InvalidateBriefingCache();
}
