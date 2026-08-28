using Foundation;
using NigerianNewGrid.Services;
using UserNotifications;

namespace NigerianNewGrid.Platforms.MacCatalyst;

public class NotificationService : INotificationService
{
    private const string MorningBriefingRequestId = "morning_briefing_request";

    public async Task<bool> RequestPermissionAsync()
    {
        try
        {
            var (granted, error) = await UNUserNotificationCenter.Current.RequestAuthorizationAsync(
                UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound);

            return granted && error == null;
        }
        catch
        {
            return false;
        }
    }

    public void ScheduleDailyMorningBriefing(TimeSpan deliveryTime)
    {
        var content = new UNMutableNotificationContent
        {
            Title = "☀️ Good Morning!",
            Body = "Your Nigerian news briefing is ready. Catch up on top stories across Politics, Business & Sports.",
            Sound = UNNotificationSound.Default
        };

        var dateComponents = new NSDateComponents
        {
            Hour = deliveryTime.Hours,
            Minute = deliveryTime.Minutes
        };

        var trigger = UNCalendarNotificationTrigger.CreateTrigger(dateComponents, repeats: true);
        var request = UNNotificationRequest.FromIdentifier(MorningBriefingRequestId, content, trigger);

        UNUserNotificationCenter.Current.AddNotificationRequest(request, null);
    }

    public void CancelDailyMorningBriefing()
    {
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests([MorningBriefingRequestId]);
    }

    public void ShowBriefingNotification(string title, string message)
    {
        PostImmediateNotification("morning_briefing_immediate", title, message, null, null, null);
    }

    public void ShowKeywordAlertNotification(string keyword, string articleTitle, string articleId, string? articleUrl = null, string? category = null)
    {
        var title = $"🔔 {keyword} — Breaking News";
        PostImmediateNotification($"keyword_{Guid.NewGuid():N}", title, articleTitle, articleId, articleUrl, category);
    }

    private static void PostImmediateNotification(string requestId, string title, string message, string? articleId, string? articleUrl = null, string? category = null)
    {
        var content = new UNMutableNotificationContent
        {
            Title = title,
            Body = message,
            Sound = UNNotificationSound.Default
        };

        var dict = new NSMutableDictionary();
        if (!string.IsNullOrEmpty(articleId)) dict.SetValueForKey(new NSString(articleId), new NSString("article_id"));
        if (!string.IsNullOrEmpty(articleUrl)) dict.SetValueForKey(new NSString(articleUrl), new NSString("article_url"));
        if (!string.IsNullOrEmpty(title)) dict.SetValueForKey(new NSString(title), new NSString("article_title"));
        if (!string.IsNullOrEmpty(category)) dict.SetValueForKey(new NSString(category), new NSString("article_category"));
        content.UserInfo = dict;

        var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(1, repeats: false);
        var request = UNNotificationRequest.FromIdentifier(requestId, content, trigger);

        UNUserNotificationCenter.Current.AddNotificationRequest(request, null);
    }
}
