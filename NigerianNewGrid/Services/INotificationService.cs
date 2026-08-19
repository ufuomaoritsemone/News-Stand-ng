namespace NigerianNewGrid.Services;

/// <summary>
/// Cross-platform abstraction for scheduling morning briefings and posting breaking keyword notifications.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Requests notification permissions on platforms that require runtime consent (Android 13+, iOS).
    /// </summary>
    Task<bool> RequestPermissionAsync();

    /// <summary>
    /// Schedules the repeating daily morning briefing at the designated time of day.
    /// </summary>
    void ScheduleDailyMorningBriefing(TimeSpan deliveryTime);

    /// <summary>
    /// Cancels any scheduled daily morning briefings.
    /// </summary>
    void CancelDailyMorningBriefing();

    /// <summary>
    /// Immediately dispatches a morning briefing notification.
    /// </summary>
    void ShowBriefingNotification(string title, string message);

    /// <summary>
    /// Immediately dispatches a high-priority keyword alert notification.
    /// </summary>
    void ShowKeywordAlertNotification(string keyword, string articleTitle, string articleId);
}
