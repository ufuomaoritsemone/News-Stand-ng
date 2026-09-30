using Android.App;
using Android.Content;
using Android.OS;
using NigerianNewGrid.Services;

namespace NigerianNewGrid.Platforms.Android;

/// <summary>
/// Android background synchronization manager using AlarmManager and WorkManager-style constraints.
/// Periodically wakes up the NewsSyncReceiver even during Android Doze mode using SetAndAllowWhileIdle.
/// </summary>
public class BackgroundSyncService : IBackgroundSyncService
{
    public const int NewsSyncAlarmRequestCode = 1003;
    public static readonly TimeSpan DefaultSyncInterval = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan ImmediateDelay = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);

    public void ScheduleNewsSync(bool immediate = false)
    {
        if (!NotificationPreferences.BackgroundUpdatesEnabled) return;

        var context = global::Android.App.Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(context, typeof(NewsSyncReceiver));
        var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetBroadcast(context, NewsSyncAlarmRequestCode, intent, pendingIntentFlags);
        if (pendingIntent == null) return;

        var delay = immediate ? ImmediateDelay : DefaultSyncInterval;
        var targetTime = DateTime.UtcNow.Add(delay);
        var triggerMillis = (long)(targetTime - DateTime.UnixEpoch).TotalMilliseconds;

        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
            }
            else
            {
                alarmManager.Set(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
            }

            System.Diagnostics.Debug.WriteLine($"[Android BackgroundSyncService] Scheduled news sync in {delay.TotalMinutes:F1} minutes (target UTC: {targetTime:HH:mm:ss}).");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Android BackgroundSyncService] Failed to schedule sync: {ex.Message}");
        }
    }

    public void CancelNewsSync()
    {
        var context = global::Android.App.Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(context, typeof(NewsSyncReceiver));
        var pendingIntentFlags = (Build.VERSION.SdkInt >= BuildVersionCodes.S)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetBroadcast(context, NewsSyncAlarmRequestCode, intent, pendingIntentFlags);
        if (pendingIntent != null)
        {
            alarmManager.Cancel(pendingIntent);
        }

        System.Diagnostics.Debug.WriteLine("[Android BackgroundSyncService] Cancelled pending background news sync.");
    }

    public void TriggerWidgetRefresh()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var widgetIntent = new Intent(context, typeof(NewsWidgetProvider));
            widgetIntent.SetAction(NewsWidgetProvider.ActionWidgetRefresh);
            context.SendBroadcast(widgetIntent);
            System.Diagnostics.Debug.WriteLine("[Android BackgroundSyncService] Broadcasted ActionWidgetRefresh to NewsWidgetProvider.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Android BackgroundSyncService] TriggerWidgetRefresh error: {ex.Message}");
        }
    }
}
