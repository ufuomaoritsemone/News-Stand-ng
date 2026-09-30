using Android.App;
using Android.Content;
using NigerianNewGrid.Services;

namespace NigerianNewGrid.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter([Intent.ActionBootCompleted])]
public class BootCompletedReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent?.Action != Intent.ActionBootCompleted) return;

        if (NotificationPreferences.MorningBriefingEnabled)
        {
            var notificationService = IPlatformApplication.Current?.Services.GetService<INotificationService>()
                ?? new NotificationService();

            notificationService.ScheduleDailyMorningBriefing(NotificationPreferences.MorningBriefingTime);
        }

        if (NotificationPreferences.AudioBriefingsEnabled)
        {
            var notificationService = IPlatformApplication.Current?.Services.GetService<INotificationService>()
                ?? new NotificationService();

            notificationService.ScheduleAudioBriefings();
        }

        if (NotificationPreferences.BackgroundUpdatesEnabled)
        {
            var syncService = IPlatformApplication.Current?.Services?.GetService<IBackgroundSyncService>()
                ?? new BackgroundSyncService();

            syncService.ScheduleNewsSync(immediate: false);
        }
    }
}
