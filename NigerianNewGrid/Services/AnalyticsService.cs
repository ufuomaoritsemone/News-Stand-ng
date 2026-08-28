using System.Diagnostics;
using System.Net.Http.Json;

namespace NigerianNewGrid.Services;

/// <summary>
/// Anonymised user behaviour event batching service.
/// 
/// - Reads the "analytics_enabled" Preference (default true).
/// - Maintains an in-memory queue; flushes when queue reaches 10 or on explicit FlushAsync().
/// - Identifies the device with a stable random UUID (no PII).
/// - All HTTP calls are fire-and-forget — failures are swallowed silently.
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private const string AnalyticsEnabledKey = "analytics_enabled";
    private const string DeviceIdKey = "analytics_device_id";
    private const int FlushThreshold = 10;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<AnalyticsEventPayload> _queue = [];
    private string? _cachedApiBase;

    private string DeviceId
    {
        get
        {
            var id = Preferences.Get(DeviceIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(id)) return id;
            id = Guid.NewGuid().ToString("N");
            Preferences.Set(DeviceIdKey, id);
            return id;
        }
    }

    private static bool IsEnabled => Preferences.Get(AnalyticsEnabledKey, defaultValue: true);

    private static string Platform => DeviceInfo.Platform.ToString();
    private static string AppVersion => AppInfo.VersionString;

    public AnalyticsService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public async Task TrackAsync(
        string eventType,
        string? articleId = null,
        string? articleTitle = null,
        string? category = null)
    {
        if (!IsEnabled) return;

        var payload = new AnalyticsEventPayload
        {
            DeviceId = DeviceId,
            EventType = eventType,
            ArticleId = articleId,
            ArticleTitle = articleTitle,
            Category = category,
            Platform = Platform,
            AppVersion = AppVersion,
            OccurredAt = DateTimeOffset.UtcNow
        };

        await _lock.WaitAsync();
        try
        {
            _queue.Add(payload);
            if (_queue.Count >= FlushThreshold)
            {
                // Hand off to background — don't block the caller
                var toSend = _queue.ToList();
                _queue.Clear();
                _ = Task.Run(() => SendAsync(toSend));
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task FlushAsync()
    {
        if (!IsEnabled) return;

        List<AnalyticsEventPayload> toSend;
        await _lock.WaitAsync();
        try
        {
            if (_queue.Count == 0) return;
            toSend = _queue.ToList();
            _queue.Clear();
        }
        finally
        {
            _lock.Release();
        }

        await SendAsync(toSend);
    }

    // ──────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────

    private async Task SendAsync(List<AnalyticsEventPayload> events)
    {
        try
        {
            var baseUrl = await GetApiBaseUrlAsync();
            if (string.IsNullOrWhiteSpace(baseUrl)) return;

            var client = _httpClientFactory.CreateClient("ProbeClient");
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.PostAsJsonAsync(
                $"{baseUrl.TrimEnd('/')}/api/v1/analytics",
                events);

            if (response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[Analytics] Sent {events.Count} event(s) successfully.");
            }
            else
            {
                Debug.WriteLine($"[Analytics] Server returned {(int)response.StatusCode} — events dropped.");
            }
        }
        catch (Exception ex)
        {
            // Never surface analytics failures to the user
            Debug.WriteLine($"[Analytics] Send failed (non-fatal): {ex.Message}");
        }
    }

    private async Task<string> GetApiBaseUrlAsync()
    {
        // Return cached value if we already resolved it
        if (!string.IsNullOrWhiteSpace(_cachedApiBase)) return _cachedApiBase;

        // Check Preferences (set by MainPage's resolver)
        var saved = Preferences.Get("api_base_url", string.Empty);
        if (!string.IsNullOrWhiteSpace(saved))
        {
            _cachedApiBase = saved;
            return saved;
        }

        // Fallback: platform-appropriate default
        _cachedApiBase = DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:56193"
            : "http://localhost:56193";

        return _cachedApiBase;
    }
}

/// <summary>Payload DTO sent to POST /api/v1/analytics</summary>
internal class AnalyticsEventPayload
{
    public string DeviceId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? ArticleId { get; set; }
    public string? ArticleTitle { get; set; }
    public string? Category { get; set; }
    public string? Platform { get; set; }
    public string? AppVersion { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
