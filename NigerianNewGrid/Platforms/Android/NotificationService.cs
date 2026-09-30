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
    private const string AudioBriefingChannelId = "audio_briefings";
    private const int MorningAlarmRequestCode = 1001;
    private const int AudioAlarmRequestCode = 1002;

    public NotificationService()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            CreateNotificationChannels();
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android26.0")]
    private static void CreateNotificationChannels()
    {

        var context = global::Android.App.Application.Context;
        var notificationManager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        if (notificationManager == null) return;

        // Morning Briefings Channel
        var morningChannel = new NotificationChannel(
            MorningChannelId,
            "Daily Morning Briefings",
            NotificationImportance.Default)
        {
            Description = "Daily curated morning news briefings for News Stand NG."
        };
        morningChannel.EnableLights(true);
        morningChannel.EnableVibration(true);

        // Audio Briefings Channel (High Priority with Sound)
        var audioChannel = new NotificationChannel(
            AudioBriefingChannelId,
            "Audio Briefings",
            NotificationImportance.High)
        {
            Description = "Twice-daily audio briefing alerts for morning and evening news digests."
        };
        audioChannel.EnableLights(true);
        audioChannel.EnableVibration(true);

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
        notificationManager.CreateNotificationChannel(audioChannel);
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
        if (pendingIntent == null) return;

        var now = DateTime.Now;
        var target = DateTime.Today.Add(deliveryTime);
        if (target <= now)
        {
            target = target.AddDays(1);
        }

        var triggerMillis = (long)(target.ToUniversalTime() - DateTime.UnixEpoch).TotalMilliseconds;

        ScheduleAlarmSafe(alarmManager, triggerMillis, pendingIntent);
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
        if (pendingIntent != null)
        {
            alarmManager.Cancel(pendingIntent);
        }
    }

    public void ScheduleAudioBriefings()
    {
        var context = global::Android.App.Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(context, typeof(AudioBriefingReceiver));
        var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetBroadcast(context, AudioAlarmRequestCode, intent, pendingIntentFlags);
        if (pendingIntent == null) return;

        var nextSlotUtc = NotificationPreferences.GetNextAudioBriefingUtc();
        var triggerMillis = (long)(nextSlotUtc - DateTime.UnixEpoch).TotalMilliseconds;

        ScheduleAlarmSafe(alarmManager, triggerMillis, pendingIntent);
    }

    private static void ScheduleAlarmSafe(AlarmManager alarmManager, long triggerMillis, PendingIntent pendingIntent)
    {
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                if (alarmManager.CanScheduleExactAlarms())
                {
                    alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
                }
                else
                {
                    // Fall back to inexact alarm if exact alarm permission was revoked / not granted
                    alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
                }
            }
            else if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
            }
            else
            {
                alarmManager.SetExact(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
            }
        }
        catch (Java.Lang.SecurityException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Android NotificationService] SecurityException scheduling exact alarm: {ex.Message}. Falling back to inexact alarm.");
            try
            {
                alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
            }
            catch (Exception fallbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"[Android NotificationService] Fallback alarm schedule failed: {fallbackEx.Message}");
            }
        }
    }

    public void CancelAudioBriefings()
    {
        var context = global::Android.App.Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(context, typeof(AudioBriefingReceiver));
        var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetBroadcast(context, AudioAlarmRequestCode, intent, pendingIntentFlags);
        if (pendingIntent != null)
        {
            alarmManager.Cancel(pendingIntent);
        }
    }

    public void ShowBriefingNotification(string title, string message)
    {
        PostNotification(MorningChannelId, 2001, title, message, null, null, null, null);
    }

    public void ShowAudioBriefingNotification(string timeOfDay, string formattedDate)
    {
        var title = timeOfDay.Equals("morning", StringComparison.OrdinalIgnoreCase)
            ? "☀️ Morning Audio Briefing Available"
            : "🌙 Evening Audio Briefing Available";

        var message = $"Your {timeOfDay} headline briefing for today {formattedDate} is ready. Tap to listen.";
        PostNotification(AudioBriefingChannelId, 2002, title, message, null, null, null, "play_audio_briefing");
    }

    public void ShowKeywordAlertNotification(string keyword, string articleTitle, string articleId, string? articleUrl = null, string? category = null)
    {
        var title = $"🔔 {keyword} — Breaking News";
        PostNotification(KeywordChannelId, (int)(DateTime.UtcNow.Ticks % int.MaxValue), title, articleTitle, articleId, articleUrl, category, null);
    }

    private static void PostNotification(
        string channelId,
        int notificationId,
        string title,
        string message,
        string? articleId,
        string? articleUrl = null,
        string? category = null,
        string? action = null)
    {
        var context = global::Android.App.Application.Context;
        var launchIntent = new Intent(context, typeof(MainActivity));
        launchIntent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        if (!string.IsNullOrEmpty(action)) launchIntent.PutExtra("action", action);
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

#pragma warning disable CS8602 // Dereference of a possibly null reference for fluent builder
        var builder = new NotificationCompat.Builder(context, channelId)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetSmallIcon(smallIcon)
            .SetAutoCancel(true)
            .SetContentIntent(pendingIntent)
            .SetPriority((int)NotificationPriority.High);

        if (channelId == KeywordChannelId || channelId == AudioBriefingChannelId)
        {
            builder.SetDefaults((int)NotificationDefaults.All);
        }

        NotificationManagerCompat.From(context).Notify(notificationId, builder.Build());
#pragma warning restore CS8602
    }
}
