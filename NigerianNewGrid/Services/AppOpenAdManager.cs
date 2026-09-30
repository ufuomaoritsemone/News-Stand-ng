using Microsoft.Extensions.Logging;
using Plugin.AdMob.Services;
using NigerianNewGrid.Constants;

namespace NigerianNewGrid.Services;

public interface IAppOpenAdManager
{
    Task PrepareAdAsync();
    Task ShowAdIfAvailableAsync();
    bool IsAdAvailable { get; }
}

/// <summary>
/// Manages App Open Ads using Plugin.AdMob's IAppOpenAdService.
/// Enforces a 4-hour frequency cap and skips display when TTS is active.
/// </summary>
public class AppOpenAdManager : IAppOpenAdManager
{
    private const string LastShownPreferenceKey = "AppOpenAd_LastShownTimeUtc";

#if DEBUG
    // In DEBUG mode, use a short 30-second interval so the developer can verify App Open Ads easily
    private static readonly TimeSpan MinimumIntervalBetweenAds = TimeSpan.FromSeconds(30);
#else
    // In PRODUCTION, enforce a 4-hour frequency cap to maintain an optimal user experience
    private static readonly TimeSpan MinimumIntervalBetweenAds = TimeSpan.FromHours(4);
#endif

    private readonly IAppOpenAdService _appOpenAdService;
    private readonly IAnalyticsService _analytics;
    private readonly ITextToSpeechService _textToSpeechService;
    private readonly ILogger<AppOpenAdManager> _logger;

#pragma warning disable CS0414
    // Prevents re-triggering while an ad is currently presented or dismissing
    private bool _isShowingAd;
#pragma warning restore CS0414

    public AppOpenAdManager(
        IAppOpenAdService appOpenAdService,
        IAnalyticsService analytics,
        ITextToSpeechService textToSpeechService,
        ILogger<AppOpenAdManager> logger)
    {
        _appOpenAdService = appOpenAdService;
        _analytics = analytics;
        _textToSpeechService = textToSpeechService;
        _logger = logger;

        // Wire up service-level events
        _appOpenAdService.OnAdLoaded += OnServiceAdLoaded;
    }

    public bool IsAdAvailable
    {
        get
        {
            var lastShownTicks = Preferences.Get(LastShownPreferenceKey, 0L);
            if (lastShownTicks == 0) return true;
            var lastShown = new DateTime(lastShownTicks, DateTimeKind.Utc);
            return (DateTime.UtcNow - lastShown) >= MinimumIntervalBetweenAds;
        }
    }

    /// <summary>
    /// Pre-loads the App Open Ad in the background so it is cached in memory and ready
    /// to display instantly when the user resumes the app from the background.
    /// </summary>
    public async Task PrepareAdAsync()
    {
#if ANDROID || IOS
        try
        {
            var unitId = AdConstants.AppOpenAdUnitId;
#if ANDROID
            Android.Util.Log.Info("APP_DEBUG", $"AppOpenAdManager.PrepareAdAsync unitId={unitId}");
#endif
            if (string.IsNullOrWhiteSpace(unitId)) return;

            if (_appOpenAdService.IsAdLoaded)
            {
                _logger.LogInformation("[AppOpenAd] Ad is already pre-loaded and cached.");
                return;
            }

#if ANDROID
            Android.Util.Log.Info("APP_DEBUG", "AppOpenAdManager calling _appOpenAdService.PrepareAd");
#endif
            _appOpenAdService.PrepareAd(unitId);
#if ANDROID
            Android.Util.Log.Info("APP_DEBUG", "AppOpenAdManager _appOpenAdService.PrepareAd DONE");
#endif
            _logger.LogInformation("[AppOpenAd] PrepareAd called with unit {UnitId}", unitId);
        }
        catch (Exception ex)
        {
#if ANDROID
            Android.Util.Log.Error("APP_DEBUG", $"AppOpenAdManager PrepareAd failed: {ex}");
#endif
            _logger.LogWarning(ex, "[AppOpenAd] PrepareAd failed");
        }
        await Task.CompletedTask;
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>
    /// Shows the App Open Ad on app resume if one is already pre-loaded, frequency cap is satisfied,
    /// and TTS is not active. Never displays delayed popups over the home screen.
    /// </summary>
    public async Task ShowAdIfAvailableAsync()
    {
#if ANDROID || IOS
        if (_isShowingAd)
        {
            _logger.LogInformation("[AppOpenAd] Ad is currently presented or dismissing; skipping duplicate request.");
            return;
        }

        if (_textToSpeechService.IsSpeaking)
        {
            _ = _analytics.TrackAsync("app_open_ad_skipped_tts_active");
            return;
        }

        if (!IsAdAvailable)
        {
            _ = _analytics.TrackAsync("app_open_ad_skipped_frequency_cap");
            return;
        }

        // If no ad is cached yet, request one for the next resume event rather than delaying or interrupting the UI
        if (!_appOpenAdService.IsAdLoaded)
        {
            _logger.LogInformation("[AppOpenAd] No ad preloaded for this resume; initiating background pre-load for next event.");
            await PrepareAdAsync();
            return;
        }

        await ShowLoadedAdAsync();
#else
        await Task.CompletedTask;
#endif
    }

    private async Task ShowLoadedAdAsync()
    {
        try
        {
            _isShowingAd = true;
            Preferences.Set(LastShownPreferenceKey, DateTime.UtcNow.Ticks);

            // Execute on MainThread to guarantee safe Android/iOS view-hierarchy presentation
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _appOpenAdService.ShowAd();
            });

            _ = _analytics.TrackAsync("app_open_ad_shown");
            _logger.LogInformation("[AppOpenAd] ShowAd presented successfully.");

            // Once the ad is displayed, schedule background pre-loading of the next ad after a brief delay
            _ = Task.Run(async () =>
            {
                await Task.Delay(2500);
                _isShowingAd = false;
                await PrepareAdAsync();
            });
        }
        catch (Exception ex)
        {
            _isShowingAd = false;
            _logger.LogError(ex, "[AppOpenAd] ShowAd failed");
            _ = _analytics.TrackAsync("app_open_ad_error", category: ex.Message);
        }
    }

    private void OnServiceAdLoaded(object? sender, EventArgs e)
    {
        _logger.LogInformation("[AppOpenAd] Ad loaded and cached in memory ready for next foreground resume.");
        _ = _analytics.TrackAsync("app_open_ad_loaded");
    }
}
