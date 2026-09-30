namespace NigerianNewGrid.Services;

/// <summary>
/// Defines periodic background synchronization for news feed retrieval,
/// cache hydration, widget refreshing, and on-device keyword alert evaluation.
/// </summary>
public interface IBackgroundSyncService
{
    /// <summary>
    /// Schedules periodic background news synchronization.
    /// </summary>
    /// <param name="immediate">If true, initiates an initial sync within a few seconds instead of waiting for the full periodic interval.</param>
    void ScheduleNewsSync(bool immediate = false);

    /// <summary>
    /// Cancels all scheduled background news synchronization alarms and tasks.
    /// </summary>
    void CancelNewsSync();

    /// <summary>
    /// Triggers an immediate widget refresh so the home screen mirrors the latest news.
    /// </summary>
    void TriggerWidgetRefresh();
}
