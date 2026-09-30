using NigerianNewGrid.Constants;
using NigerianNewGrid.Services;

namespace NigerianNewGrid;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAnalyticsService _analytics;
    private readonly IAppOpenAdManager _appOpenAdManager;

    public App(IServiceProvider serviceProvider, IAnalyticsService analytics, IAppOpenAdManager appOpenAdManager)
    {
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", "App constructor START");
#endif
        _serviceProvider = serviceProvider;
        _analytics = analytics;
        _appOpenAdManager = appOpenAdManager;
        InitializeComponent();
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", "App InitializeComponent DONE");
#endif
    }

    private bool _isColdStartCompleted;

    protected override Window CreateWindow(IActivationState? activationState)
    {
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", "App.CreateWindow STARTED");
#endif
        var hasSeenOnboarding = Preferences.Get(AppPreferenceKeys.HasSeenOnboarding, false);
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", $"App.CreateWindow hasSeenOnboarding = {hasSeenOnboarding}");
#endif

        Page rootPage;
        try
        {
            rootPage = hasSeenOnboarding
                ? _serviceProvider.GetRequiredService<AppShell>()
                : _serviceProvider.GetRequiredService<OnboardingPage>();
#if ANDROID
            Android.Util.Log.Info("APP_DEBUG", $"App.CreateWindow rootPage resolved: {rootPage?.GetType().Name}");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"App.CreateWindow ERROR resolving rootPage: {ex}");
#if ANDROID
            Android.Util.Log.Error("APP_DEBUG", $"App.CreateWindow ERROR resolving rootPage: {ex}");
#endif
            throw;
        }

        var window = new Window(rootPage!);
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", "App.CreateWindow Window created successfully");
#endif

        // Track app opens on resume (foreground) and check for App Open Ads (warm start only)
        window.Resumed += (_, _) =>
        {
            if (!_isColdStartCompleted) return;
            _ = _analytics.TrackAsync("app_open");
            _ = _appOpenAdManager.ShowAdIfAvailableAsync();
            AppNotificationBridge.TriggerAppResumed();
        };

        return window;
    }

    protected override void OnStart()
    {
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", "App.OnStart START");
#endif
        base.OnStart();
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", "App.OnStart base.OnStart DONE");
#endif
        // Initialize SQLite news cache in background on startup
        var persistence = _serviceProvider.GetService<INewsPersistenceService>();
        _ = persistence?.InitializeAsync();

        // Track initial cold-start; pre-load the App Open Ad silently in background so home screen is instant
        _ = _analytics.TrackAsync("app_open");
        _ = _appOpenAdManager.PrepareAdAsync();

        // Ensure background sync schedule is active if background updates are enabled
        if (NotificationPreferences.BackgroundUpdatesEnabled)
        {
            var syncService = _serviceProvider.GetService<IBackgroundSyncService>();
            syncService?.ScheduleNewsSync(immediate: false);
        }

        _isColdStartCompleted = true;
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", "App.OnStart FINISHED");
#endif
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        // Flush any pending events when the app moves to background
        _ = _analytics.FlushAsync();

        // Trigger immediate widget refresh so home screen mirrors latest stories
        var syncService = _serviceProvider.GetService<IBackgroundSyncService>();
        syncService?.TriggerWidgetRefresh();
    }
}

