using Android.App;
using Android.Content;
using NigerianNewGrid.Services;

namespace NigerianNewGrid.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public class MorningBriefingReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;
        if (!NotificationPreferences.MorningBriefingEnabled) return;

        var notificationService = IPlatformApplication.Current?.Services.GetService<INotificationService>()
            ?? new NotificationService();

        notificationService.ShowBriefingNotification(
            "☀️ Good Morning!",
            "Your Nigerian news briefing is ready. Catch up on top stories across Politics, Business & Sports.");

        // Reschedule for the next day
        notificationService.ScheduleDailyMorningBriefing(NotificationPreferences.MorningBriefingTime);
    }
}
