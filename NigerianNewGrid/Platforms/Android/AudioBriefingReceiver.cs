using Android.App;
using Android.Content;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Helpers;
using NigerianNewGrid.Services;

namespace NigerianNewGrid.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public class AudioBriefingReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;
        if (!NotificationPreferences.AudioBriefingsEnabled) return;

        var wat = TtsBriefingFormatter.GetNigerianTime();
        var timeOfDay = TtsBriefingFormatter.GetTimeOfDay(wat);
        var formattedDate = TtsBriefingFormatter.FormatBriefingDate(wat);
        var cycle = NotificationPreferences.GetCurrentAudioBriefingCycle();

        // Prevent duplicate alerts for the same cycle
        if (string.Equals(NotificationPreferences.LastAudioBriefingCycleNotified, cycle, StringComparison.OrdinalIgnoreCase))
        {
            var service = IPlatformApplication.Current?.Services.GetService<INotificationService>() ?? new NotificationService();
            service.ScheduleAudioBriefings();
            return;
        }

        var pendingResult = GoAsync();

        _ = Task.Run(async () =>
        {
            try
            {
                var services = IPlatformApplication.Current?.Services;
                var apiClient = services?.GetService<NewsApiClient>();
                var cacheService = services?.GetService<IBriefingCacheService>();
                var notificationService = services?.GetService<INotificationService>() ?? new NotificationService();

                bool headlinesAvailable = false;

                if (apiClient != null)
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                    headlinesAvailable = await apiClient.CheckAudioHeadlinesAvailableAsync(cts.Token);
                }

                if (!headlinesAvailable && cacheService != null)
                {
                    var cached = await cacheService.GetCachedBriefingAsync();
                    headlinesAvailable = cached.Any(c => c.Top.Count > 0);
                }

                if (headlinesAvailable)
                {
                    NotificationPreferences.LastAudioBriefingCycleNotified = cycle;
                    notificationService.ShowAudioBriefingNotification(timeOfDay, formattedDate);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[AudioBriefingReceiver] Skipped notification: no curated audio headlines available for cycle {cycle}.");
                }

                // Reschedule for the next slot (Morning <-> Evening)
                notificationService.ScheduleAudioBriefings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioBriefingReceiver] Error checking audio headlines availability: {ex.Message}");
            }
            finally
            {
                pendingResult?.Finish();
            }
        });
    }
}
