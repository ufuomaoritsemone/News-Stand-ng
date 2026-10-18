using Polly;
using Polly.Extensions.Http;
using NigerianNewsGrid.Client;
using NigerianNewGrid.Services;
using NigerianNewGrid.ViewModels;

namespace NigerianNewGrid.Extensions;

/// <summary>
/// Clean modular registration of app services, HTTP clients, platform abstractions, ViewModels, and pages (Issue #23).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNewsHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient<NewsApiClient>((sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        })
        .AddPolicyHandler(HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
        .AddPolicyHandler(Policy<HttpResponseMessage>
            .Handle<Exception>()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(10)));

        services.AddHttpClient("ProbeClient", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        return services;
    }

    public static IServiceCollection AddNewsServices(this IServiceCollection services)
    {
        // Persistence & Repositories (ISP-compliant)
        services.AddSingleton<NewsPersistenceService>();
        services.AddSingleton<INewsPersistenceService>(sp => sp.GetRequiredService<NewsPersistenceService>());
        services.AddSingleton<IStoryRepository>(sp => sp.GetRequiredService<NewsPersistenceService>());
        services.AddSingleton<IBookmarkRepository>(sp => sp.GetRequiredService<NewsPersistenceService>());
        services.AddSingleton<IReadHistoryRepository>(sp => sp.GetRequiredService<NewsPersistenceService>());

        // Core Domain & UI Services
        services.AddSingleton<IUserPreferencesService, UserPreferencesService>();
        services.AddSingleton<IAppEventBus, AppEventBus>();
        services.AddSingleton<ITextToSpeechService, MauiTextToSpeechService>();
        services.AddSingleton<IBookmarkService, BookmarkService>();
        services.AddSingleton<IRecentlyReadService, RecentlyReadService>();
        services.AddSingleton<IBriefingCacheService, BriefingCacheService>();
        services.AddSingleton<IAnalyticsService, AnalyticsService>();
        services.AddSingleton<IAppOpenAdManager, AppOpenAdManager>();
        services.AddSingleton<ISonicFeedbackService, SonicFeedbackService>();
        services.AddSingleton<IXShareService, XShareService>();
        services.AddSingleton<IKeywordMatchingService, KeywordMatchingService>();
        services.AddSingleton<IKeywordAlertCoordinator, KeywordAlertCoordinator>();
        services.AddSingleton<IAudioBriefingCoordinator, AudioBriefingCoordinator>();
        services.AddSingleton<IArticleInteractionService, ArticleInteractionService>();

        return services;
    }

    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
#if ANDROID
        services.AddSingleton<INotificationService, Platforms.Android.NotificationService>();
        services.AddSingleton<IBackgroundSyncService, Platforms.Android.BackgroundSyncService>();
#elif IOS
        services.AddSingleton<INotificationService, Platforms.iOS.NotificationService>();
        services.AddSingleton<IBackgroundSyncService, NullBackgroundSyncService>();
#elif MACCATALYST
        services.AddSingleton<INotificationService, Platforms.MacCatalyst.NotificationService>();
        services.AddSingleton<IBackgroundSyncService, NullBackgroundSyncService>();
#elif WINDOWS
        services.AddSingleton<INotificationService, Platforms.Windows.NotificationService>();
        services.AddSingleton<IBackgroundSyncService, NullBackgroundSyncService>();
#else
        services.AddSingleton<INotificationService, NullNotificationService>();
        services.AddSingleton<IBackgroundSyncService, NullBackgroundSyncService>();
#endif
        return services;
    }

    public static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        // Tab-level VMs are Singleton because Shell tabs persist for the app's lifetime.
        // Transient registration caused event handler accumulation and memory leaks since
        // these VMs subscribe to singleton events (Connectivity, AudioCoordinator, EventBus).
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DiscoverViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // ArticleViewModel and StoryClusterViewModel stay Transient — each page is an independent session.
        services.AddTransient<ArticleViewModel>();
        services.AddTransient<StoryClusterViewModel>();
        return services;
    }

    public static IServiceCollection AddPages(this IServiceCollection services)
    {
        services.AddTransient<MainPage>();
        services.AddTransient<ArticleWebPage>();
        services.AddTransient<StoryClusterPage>();
        services.AddTransient<BookmarksPage>();
        services.AddTransient<DiscoverPage>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<OnboardingPage>();
        services.AddSingleton<AppShell>();
        return services;
    }
}
