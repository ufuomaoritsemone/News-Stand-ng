using Foundation;
using NigerianNewGrid.Services;
using UserNotifications;

namespace NigerianNewGrid.Platforms.iOS;

public class NotificationService : INotificationService
{
    private const string MorningBriefingRequestId = "morning_briefing_request";
    private const string MorningAudioBriefingRequestId = "audio_briefing_morning_request";
    private const string EveningAudioBriefingRequestId = "audio_briefing_evening_request";

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

        UNUserNotificationCenter.Current.AddNotificationRequest(request, err =>
        {
            if (err != null)
            {
                System.Diagnostics.Debug.WriteLine($"[iOS NotificationService] Schedule error: {err.LocalizedDescription}");
            }
        });
    }

    public void CancelDailyMorningBriefing()
    {
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests([MorningBriefingRequestId]);
    }

    public void ScheduleAudioBriefings()
    {
        // 8:00 AM WAT is 7:00 AM UTC; 6:00 PM WAT is 5:00 PM UTC
        var morningUtc = DateTime.UtcNow.Date.AddHours(7);
        var morningLocal = morningUtc.ToLocalTime();

        var eveningUtc = DateTime.UtcNow.Date.AddHours(17);
        var eveningLocal = eveningUtc.ToLocalTime();

        // 1. Morning Audio Briefing (8:00 AM WAT)
        var morningContent = new UNMutableNotificationContent
        {
            Title = "☀️ Morning Audio Briefing Available",
            Body = "Your morning headline briefing is ready. Tap to listen.",
            Sound = UNNotificationSound.Default
        };
        var morningDict = new NSMutableDictionary();
        morningDict.SetValueForKey(new NSString("play_audio_briefing"), new NSString("action"));
        morningContent.UserInfo = morningDict;

        var morningComponents = new NSDateComponents
        {
            Hour = morningLocal.Hour,
            Minute = morningLocal.Minute
        };
        var morningTrigger = UNCalendarNotificationTrigger.CreateTrigger(morningComponents, repeats: true);
        var morningReq = UNNotificationRequest.FromIdentifier(MorningAudioBriefingRequestId, morningContent, morningTrigger);
        UNUserNotificationCenter.Current.AddNotificationRequest(morningReq, null);

        // 2. Evening Audio Briefing (6:00 PM WAT)
        var eveningContent = new UNMutableNotificationContent
        {
            Title = "🌙 Evening Audio Briefing Available",
            Body = "Your evening headline briefing is ready. Tap to listen.",
            Sound = UNNotificationSound.Default
        };
        var eveningDict = new NSMutableDictionary();
        eveningDict.SetValueForKey(new NSString("play_audio_briefing"), new NSString("action"));
        eveningContent.UserInfo = eveningDict;

        var eveningComponents = new NSDateComponents
        {
            Hour = eveningLocal.Hour,
            Minute = eveningLocal.Minute
        };
        var eveningTrigger = UNCalendarNotificationTrigger.CreateTrigger(eveningComponents, repeats: true);
        var eveningReq = UNNotificationRequest.FromIdentifier(EveningAudioBriefingRequestId, eveningContent, eveningTrigger);
        UNUserNotificationCenter.Current.AddNotificationRequest(eveningReq, null);
    }

    public void CancelAudioBriefings()
    {
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests([MorningAudioBriefingRequestId, EveningAudioBriefingRequestId]);
    }

    public void ShowBriefingNotification(string title, string message)
    {
        PostImmediateNotification("morning_briefing_immediate", title, message, null, null, null, null);
    }

    public void ShowAudioBriefingNotification(string timeOfDay, string formattedDate)
    {
        var title = timeOfDay.Equals("morning", StringComparison.OrdinalIgnoreCase)
            ? "☀️ Morning Audio Briefing Available"
            : "🌙 Evening Audio Briefing Available";

        var message = $"Your {timeOfDay} headline briefing for today {formattedDate} is ready. Tap to listen.";
        PostImmediateNotification("audio_briefing_immediate", title, message, null, null, null, "play_audio_briefing");
    }

    public void ShowKeywordAlertNotification(string keyword, string articleTitle, string articleId, string? articleUrl = null, string? category = null)
    {
        var title = $"🔔 {keyword} — Breaking News";
        PostImmediateNotification($"keyword_{Guid.NewGuid():N}", title, articleTitle, articleId, articleUrl, category, null);
    }

    private static void PostImmediateNotification(string requestId, string title, string message, string? articleId, string? articleUrl = null, string? category = null, string? action = null)
    {
        var content = new UNMutableNotificationContent
        {
            Title = title,
            Body = message,
            Sound = UNNotificationSound.Default
        };

        var dict = new NSMutableDictionary();
        if (!string.IsNullOrEmpty(action)) dict.SetValueForKey(new NSString(action), new NSString("action"));
        if (!string.IsNullOrEmpty(articleId)) dict.SetValueForKey(new NSString(articleId), new NSString("article_id"));
        if (!string.IsNullOrEmpty(articleUrl)) dict.SetValueForKey(new NSString(articleUrl), new NSString("article_url"));
        if (!string.IsNullOrEmpty(title)) dict.SetValueForKey(new NSString(title), new NSString("article_title"));
        if (!string.IsNullOrEmpty(category)) dict.SetValueForKey(new NSString(category), new NSString("article_category"));
        content.UserInfo = dict;

        // Trigger in 1 second
        var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(1, repeats: false);
        var request = UNNotificationRequest.FromIdentifier(requestId, content, trigger);

        UNUserNotificationCenter.Current.AddNotificationRequest(request, null);
    }
}
