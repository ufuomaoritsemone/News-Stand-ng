using NigerianNewGrid.Services;

namespace NigerianNewGrid;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAnalyticsService _analytics;

    public App(IServiceProvider serviceProvider, IAnalyticsService analytics)
    {
        _serviceProvider = serviceProvider;
        _analytics = analytics;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var shell = _serviceProvider.GetRequiredService<AppShell>();
        var window = new Window(shell);

        // Track app opens on resume (foreground)
        window.Resumed += (_, _) => _ = _analytics.TrackAsync("app_open");

        return window;
    }

    protected override void OnStart()
    {
        base.OnStart();
        // Track initial cold-start
        _ = _analytics.TrackAsync("app_open");
    }

    protected override void OnSleep()
    {
        base.OnSleep();
        // Flush any pending events when the app moves to background
        _ = _analytics.FlushAsync();
    }
}

