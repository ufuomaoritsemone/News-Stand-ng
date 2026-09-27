using System.Diagnostics;
using NigerianNewGrid.Services;

namespace NigerianNewGrid.Platforms.Windows;

public class NotificationService : INotificationService
{
    public Task<bool> RequestPermissionAsync()
    {
        return Task.FromResult(true);
    }

    public void ScheduleDailyMorningBriefing(TimeSpan deliveryTime)
    {
        Debug.WriteLine($"[Windows NotificationService] Daily Morning Briefing scheduled for {deliveryTime}");
    }

    public void CancelDailyMorningBriefing()
    {
        Debug.WriteLine("[Windows NotificationService] Daily Morning Briefing cancelled.");
    }

    public void ShowBriefingNotification(string title, string message)
    {
        Debug.WriteLine($"[Windows NotificationService] Briefing Notification: {title} | {message}");
    }

    public void ShowKeywordAlertNotification(string keyword, string articleTitle, string articleId, string? articleUrl = null, string? category = null)
    {
        Debug.WriteLine($"[Windows NotificationService] Keyword Alert [{keyword}]: {articleTitle} (ID: {articleId}, URL: {articleUrl})");
    }

    public void ScheduleAudioBriefings()
    {
        Debug.WriteLine("[Windows NotificationService] ScheduleAudioBriefings called.");
    }

    public void CancelAudioBriefings()
    {
        Debug.WriteLine("[Windows NotificationService] CancelAudioBriefings called.");
    }

    public void ShowAudioBriefingNotification(string timeOfDay, string formattedDate)
    {
        Debug.WriteLine($"[Windows NotificationService] Audio Briefing Notification: {timeOfDay} for {formattedDate}");
    }
}
