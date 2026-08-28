namespace NigerianNewGrid.Services;

/// <summary>
/// Records anonymous in-app user behaviour events and batches them to the server.
/// All tracking is subject to the user's opt-in preference (analytics_enabled).
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Queues a single event. The service will batch and send events automatically.
    /// Safe to call from any thread; never throws.
    /// </summary>
    Task TrackAsync(
        string eventType,
        string? articleId = null,
        string? articleTitle = null,
        string? category = null);

    /// <summary>
    /// Immediately attempts to flush all pending events to the server.
    /// Call on app pause / background.
    /// </summary>
    Task FlushAsync();
}
