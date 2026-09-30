using System.Text;
using System.Text.Json;
using Android.Content;

namespace NigerianNewGrid.Platforms.Android;

/// <summary>
/// Lightweight, fire-and-forget analytics helper for widget lifecycle events.
///
/// Design constraints:
/// - The widget runs in a BroadcastReceiver whose process may not be the app process,
///   so MAUI DI (IAnalyticsService / IHttpClientFactory) is unavailable.
/// - We use a bare HttpClient and read the stored preferences directly via Android
///   SharedPreferences (the same backing store MAUI Preferences writes to).
/// - All calls are best-effort: any exception is swallowed silently so the widget
///   itself is never disrupted by an analytics failure.
///
/// Events tracked:
///   widget_enabled    — first widget instance added to home screen
///   widget_disabled   — last widget instance removed from home screen
///   widget_refresh    — user tapped the refresh button on the widget
///   widget_item_click — user tapped a story card in the widget
/// </summary>
internal static class WidgetAnalyticsHelper
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    // MAUI Preferences on Android writes to a SharedPreferences file named after the app package.
    private const string PrefsName = "com.nigeriannewsgrid.app_preferences";
    private const string ApiBaseUrlKey = "api_base_url";
    private const string DeviceIdKey = "analytics_device_id";
    private const string AnalyticsEnabledKey = "analytics_enabled";

    /// <summary>Tracks a widget event asynchronously without blocking the BroadcastReceiver.</summary>
    public static void Track(
        Context context,
        string eventType,
        string? articleId = null,
        string? articleTitle = null,
        string? category = null)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await TrackInternalAsync(context, eventType, articleId, articleTitle, category);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WidgetAnalytics] Non-fatal: {ex.Message}");
            }
        });
    }

    private static async Task TrackInternalAsync(
        Context context,
        string eventType,
        string? articleId,
        string? articleTitle,
        string? category)
    {
        // Respect the analytics opt-out preference
        var prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private);
        var analyticsEnabled = prefs?.GetBoolean(AnalyticsEnabledKey, true) ?? true;
        if (!analyticsEnabled) return;

        var baseUrl = prefs?.GetString(ApiBaseUrlKey, null);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            // Widget background process — fall back to standard Android emulator loopback
            baseUrl = "http://10.0.2.2:56193";
        }

        // Re-use the same stable device ID that IAnalyticsService uses
        var deviceId = prefs?.GetString(DeviceIdKey, null);
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            // First run before the app is opened — generate and persist a transient ID
            deviceId = Guid.NewGuid().ToString("N");
            prefs?.Edit()?.PutString(DeviceIdKey, deviceId)?.Apply();
        }

        var payload = new[]
        {
            new
            {
                DeviceId   = deviceId,
                EventType  = eventType,
                ArticleId  = articleId,
                ArticleTitle = articleTitle,
                Category   = category,
                Platform   = "Android",
                AppVersion = context.PackageManager?
                    .GetPackageInfo(context.PackageName!, 0)?.VersionName ?? "unknown",
                OccurredAt = DateTimeOffset.UtcNow
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"{baseUrl.TrimEnd('/')}/api/v1/analytics";
        await _http.PostAsync(url, content);

        System.Diagnostics.Debug.WriteLine(
            $"[WidgetAnalytics] Tracked '{eventType}'" +
            (articleId != null ? $" articleId={articleId}" : ""));
    }
}
