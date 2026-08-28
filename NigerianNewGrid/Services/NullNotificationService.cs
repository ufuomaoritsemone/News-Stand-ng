using System.Diagnostics;

namespace NigerianNewGrid.Services;

/// <summary>
/// Fallback no-op notification service for unsupported environments or unit tests.
/// </summary>
public sealed class NullNotificationService : INotificationService
{
    public Task<bool> RequestPermissionAsync()
    {
        Debug.WriteLine("[NullNotificationService] RequestPermissionAsync called.");
        return Task.FromResult(true);
    }

    public void ScheduleDailyMorningBriefing(TimeSpan deliveryTime)
    {
        Debug.WriteLine($"[NullNotificationService] ScheduleDailyMorningBriefing at {deliveryTime}");
    }

    public void CancelDailyMorningBriefing()
    {
        Debug.WriteLine("[NullNotificationService] CancelDailyMorningBriefing called.");
    }

    public void ShowBriefingNotification(string title, string message)
    {
        Debug.WriteLine($"[NullNotificationService] ShowBriefingNotification: {title} - {message}");
    }

    public void ShowKeywordAlertNotification(string keyword, string articleTitle, string articleId, string? articleUrl = null, string? category = null)
    {
        Debug.WriteLine($"[NullNotificationService] ShowKeywordAlertNotification: [{keyword}] {articleTitle} ({articleId}) URL: {articleUrl}");
    }
}
