using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using NigerianNewGrid.Services;

namespace NigerianNewGrid.Platforms.Android;

public class NotificationService : INotificationService
{
    private const string MorningChannelId = "morning_briefings";
    private const string KeywordChannelId = "keyword_alerts";
    private const int MorningAlarmRequestCode = 1001;

    public NotificationService()
    {
        CreateNotificationChannels();
    }

    private static void CreateNotificationChannels()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

        var context = global::Android.App.Application.Context;
        var notificationManager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (notificationManager == null) return;

        // Morning Briefings Channel
        var morningChannel = new NotificationChannel(
            MorningChannelId,
            "Daily Morning Briefings",
            NotificationImportance.Default)
        {
            Description = "Daily curated morning news briefings for Nigerian News Grid."
        };
        morningChannel.EnableLights(true);
        morningChannel.EnableVibration(true);

        // Keyword Alerts Channel
        var keywordChannel = new NotificationChannel(
            KeywordChannelId,
            "Breaking Keyword Alerts",
            NotificationImportance.High)
        {
            Description = "High-priority breaking news matching your monitored keywords."
        };
        keywordChannel.EnableLights(true);
        keywordChannel.EnableVibration(true);

        notificationManager.CreateNotificationChannel(morningChannel);
        notificationManager.CreateNotificationChannel(keywordChannel);
    }

    public async Task<bool> RequestPermissionAsync()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            }
            return status == PermissionStatus.Granted;
        }

        return true;
    }

    public void ScheduleDailyMorningBriefing(TimeSpan deliveryTime)
    {
        var context = global::Android.App.Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(context, typeof(MorningBriefingReceiver));
        var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetBroadcast(context, MorningAlarmRequestCode, intent, pendingIntentFlags);

        var now = DateTime.Now;
        var target = DateTime.Today.Add(deliveryTime);
        if (target <= now)
        {
            target = target.AddDays(1);
        }

        var triggerMillis = (long)(target.ToUniversalTime() - DateTime.UnixEpoch).TotalMilliseconds;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
        {
            alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
        }
        else
        {
            alarmManager.SetExact(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
        }
    }

    public void CancelDailyMorningBriefing()
    {
        var context = global::Android.App.Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(context, typeof(MorningBriefingReceiver));
        var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetBroadcast(context, MorningAlarmRequestCode, intent, pendingIntentFlags);
        alarmManager.Cancel(pendingIntent);
    }

    public void ShowBriefingNotification(string title, string message)
    {
        PostNotification(MorningChannelId, 2001, title, message, null, null, null);
    }

    public void ShowKeywordAlertNotification(string keyword, string articleTitle, string articleId, string? articleUrl = null, string? category = null)
    {
        var title = $"🔔 {keyword} — Breaking News";
        PostNotification(KeywordChannelId, (int)(DateTime.UtcNow.Ticks % int.MaxValue), title, articleTitle, articleId, articleUrl, category);
    }

    private static void PostNotification(
        string channelId,
        int notificationId,
        string title,
        string message,
        string? articleId,
        string? articleUrl = null,
        string? category = null)
    {
        var context = global::Android.App.Application.Context;
        var launchIntent = new Intent(context, typeof(MainActivity));
        launchIntent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        if (!string.IsNullOrEmpty(articleId)) launchIntent.PutExtra("article_id", articleId);
        if (!string.IsNullOrEmpty(articleUrl)) launchIntent.PutExtra("article_url", articleUrl);
        if (!string.IsNullOrEmpty(message)) launchIntent.PutExtra("article_title", message);
        if (!string.IsNullOrEmpty(category)) launchIntent.PutExtra("article_category", category);

        var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetActivity(
            context,
            notificationId,
            launchIntent,
            pendingIntentFlags);

        var smallIcon = context.ApplicationInfo?.Icon != 0
            ? context.ApplicationInfo!.Icon
            : global::Android.Resource.Drawable.IcDialogInfo;

        var builder = new NotificationCompat.Builder(context, channelId)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetSmallIcon(smallIcon)
            .SetAutoCancel(true)
            .SetContentIntent(pendingIntent)
            .SetPriority((int)NotificationPriority.High);

        if (channelId == KeywordChannelId)
        {
            builder.SetDefaults((int)NotificationDefaults.All);
        }

        NotificationManagerCompat.From(context).Notify(notificationId, builder.Build());
    }
}
