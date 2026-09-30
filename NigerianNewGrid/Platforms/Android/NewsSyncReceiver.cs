using Android.App;
using Android.Content;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;
using NigerianNewGrid.Services;

namespace NigerianNewGrid.Platforms.Android;

/// <summary>
/// Autonomous background receiver triggered by AlarmManager.
/// Embodies WorkManager-grade constraints (network checks, battery saver guard, exponential backoff)
/// to fetch the latest news, refresh local cache, update home screen widgets, and dispatch
/// on-device keyword alert notifications.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public class NewsSyncReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null) return;

        // Respect user setting: abort immediately if background updates are toggled off to save battery
        if (!NotificationPreferences.BackgroundUpdatesEnabled)
        {
            var syncService = IPlatformApplication.Current?.Services?.GetService<IBackgroundSyncService>()
                ?? new BackgroundSyncService();
            syncService.CancelNewsSync();
            return;
        }

        // Constraint 1: Network connectivity check (WorkManager NetworkType.Connected guard)
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            System.Diagnostics.Debug.WriteLine("[NewsSyncReceiver] No internet connection available. Rescheduling retry in 5 minutes.");
            Reschedule(isRetry: true);
            return;
        }

        // Constraint 2: Power and battery check (WorkManager RequiresBatteryNotLow guard)
        try
        {
            if (Battery.EnergySaverStatus == EnergySaverStatus.On ||
                (Battery.ChargeLevel < 0.15 && Battery.State != BatteryState.Charging))
            {
                System.Diagnostics.Debug.WriteLine("[NewsSyncReceiver] Device in energy saver or critical battery. Deferring sync to normal interval.");
                Reschedule(isRetry: false);
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NewsSyncReceiver] Battery check warning: {ex.Message}");
        }

        var pendingResult = GoAsync();

        _ = Task.Run(async () =>
        {
            bool success = false;
            try
            {
                var services = IPlatformApplication.Current?.Services;

                // Resolve services with robust fallbacks
                var apiClient = services?.GetService<NewsApiClient>() ?? CreateFallbackApiClient();
                var cacheService = services?.GetService<IBriefingCacheService>();
                var persistenceService = services?.GetService<INewsPersistenceService>()
                    ?? new NewsPersistenceService(Microsoft.Extensions.Logging.Abstractions.NullLogger<NewsPersistenceService>.Instance);
                var keywordService = services?.GetService<IKeywordMatchingService>() ?? new KeywordMatchingService();
                var notificationService = services?.GetService<INotificationService>() ?? new NotificationService();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var categories = await apiClient.GetDailyBriefingAsync(cancellationToken: cts.Token);

                if (categories is { Count: > 0 })
                {
                    // 1. Refresh local newsfeed cache so when user opens the app, feed is instantly up to date
                    if (cacheService != null)
                    {
                        await cacheService.SaveBriefingAsync(categories);
                    }
                    else
                    {
                        Preferences.Set(AppPreferenceKeys.LastBriefingReceivedUtc, DateTime.UtcNow.ToString("O"));
                        var newCount = await persistenceService.SaveBriefingAsync(categories);
                        if (newCount > 0)
                        {
                            AppNotificationBridge.NotifyNewStoriesAvailable(newCount);
                        }
                    }

                    // 1b. Delta sync — patch any category/metadata changes that happened on
                    //     the server since the last successful sync (resolves Admin Dashboard discrepancies)
                    try
                    {
                        var lastSync = Preferences.Get(AppPreferenceKeys.LastDeltaSyncUtc, string.Empty);
                        DateTime? sinceUtc = DateTime.TryParse(lastSync, null,
                            System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                            ? parsed
                            : null;

                        using var deltaCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        var deltas = await apiClient.GetArticleDeltasAsync(sinceUtc, deltaCts.Token);

                        if (deltas is { Count: > 0 })
                        {
                            await persistenceService.ApplyDeltasAsync(deltas, deltaCts.Token);
                            System.Diagnostics.Debug.WriteLine($"[NewsSyncReceiver] Delta sync applied {deltas.Count} update(s).");
                        }

                        Preferences.Set(AppPreferenceKeys.LastDeltaSyncUtc,
                            DateTime.UtcNow.ToString("O")); // ISO 8601 round-trip
                    }
                    catch (Exception ex)
                    {
                        // Non-fatal: delta sync failure must never block the main briefing update
                        System.Diagnostics.Debug.WriteLine($"[NewsSyncReceiver] Delta sync warning: {ex.Message}");
                    }

                    // 2. Broadcast widget refresh so the home screen widget displays the newest stories
                    try
                    {
                        var widgetIntent = new Intent(context, typeof(NewsWidgetProvider));
                        widgetIntent.SetAction(NewsWidgetProvider.ActionWidgetRefresh);
                        context.SendBroadcast(widgetIntent);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[NewsSyncReceiver] Widget broadcast warning: {ex.Message}");
                    }

                    // 3. Evaluate monitored keywords against fresh stories if alerts are enabled
                    if (NotificationPreferences.KeywordAlertsEnabled)
                    {
                        var monitoredKeywords = NotificationPreferences.MonitoredKeywords;
                        if (monitoredKeywords.Count > 0)
                        {
                            var allArticles = categories
                                .SelectMany(c => c.Top)
                                .Where(s => !string.IsNullOrWhiteSpace(s.Id))
                                .ToList();

                            var matches = keywordService.EvaluateFreshArticles(
                                allArticles,
                                monitoredKeywords,
                                NotificationPreferences.LastKeywordAlertEvaluationUtc,
                                NotificationPreferences.NotifiedArticleIds);

                            foreach (var match in matches)
                            {
                                notificationService.ShowKeywordAlertNotification(
                                    match.MatchedKeyword,
                                    match.Article.Title,
                                    match.Article.Id,
                                    match.Article.Url,
                                    match.Article.Category);

                                NotificationPreferences.RecordNotifiedArticle(match.Article.Id);
                            }

                            if (matches.Count > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"[NewsSyncReceiver] Dispatched {matches.Count} keyword alert notification(s).");
                            }
                        }

                        NotificationPreferences.LastKeywordAlertEvaluationUtc = DateTime.UtcNow;
                    }

                    NotificationPreferences.LastBackgroundSyncUtc = DateTime.UtcNow;
                    success = true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[NewsSyncReceiver] Briefing API returned 0 categories.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NewsSyncReceiver] Background sync exception: {ex.Message}");
            }
            finally
            {
                // Reschedule next cycle: if failed/timed out, retry sooner (5 mins); if succeeded, normal 30 mins
                Reschedule(isRetry: !success);
                pendingResult?.Finish();
            }
        });
    }

    private static void Reschedule(bool isRetry)
    {
        if (!NotificationPreferences.BackgroundUpdatesEnabled) return;

        var context = global::Android.App.Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return;

        var intent = new Intent(context, typeof(NewsSyncReceiver));
        var pendingIntentFlags = (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.S)
            ? PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetBroadcast(context, BackgroundSyncService.NewsSyncAlarmRequestCode, intent, pendingIntentFlags);
        if (pendingIntent == null) return;

        var delay = isRetry ? BackgroundSyncService.RetryInterval : BackgroundSyncService.DefaultSyncInterval;
        var targetTime = DateTime.UtcNow.Add(delay);
        var triggerMillis = (long)(targetTime - DateTime.UnixEpoch).TotalMilliseconds;

        try
        {
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.M)
            {
                alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
            }
            else
            {
                alarmManager.Set(AlarmType.RtcWakeup, triggerMillis, pendingIntent);
            }

            System.Diagnostics.Debug.WriteLine($"[NewsSyncReceiver] Rescheduled next sync (retry: {isRetry}) in {delay.TotalMinutes:F1} mins.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NewsSyncReceiver] Failed to reschedule: {ex.Message}");
        }
    }

    private static NewsApiClient CreateFallbackApiClient()
    {
        var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, string.Empty);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:56193" : "http://localhost:56193";
        }

        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        return new NewsApiClient(client) { BaseUrl = baseUrl };
    }
}
