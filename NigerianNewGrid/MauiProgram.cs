using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using Polly;
using Polly.Extensions.Http;
using NigerianNewsGrid.Client;
using Plugin.AdMob;

namespace NigerianNewGrid
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseAdMob()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("LegacySerifITCTTBold.ttf", "LegacySerifBold");
                    fonts.AddFont("LegacySerifITCTTBold.ttf", "LegacySerifITCTTBold");
                    fonts.AddFont("Legacy Sans Book.TTF", "LegacySansBook");
                    fonts.AddFont("Legacy Sans Book.TTF", "Legacy Sans Book");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Register NewsApiClient as a typed client with IHttpClientFactory and resiliency policies.
            // NOTE: BaseAddress is intentionally NOT set here. ResolveApiBaseUrlAsync in MainViewModel
            // dynamically discovers the correct host (e.g. 10.0.2.2 on Android emulator vs localhost
            // on Windows/iOS). GetDailyBriefingAsync builds fully-qualified URIs, so BaseAddress is unused.
            builder.Services.AddHttpClient<NewsApiClient>((sp, client) =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddPolicyHandler(HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
            .AddPolicyHandler(Policy<HttpResponseMessage>
                .Handle<Exception>()
                .CircuitBreakerAsync(5, TimeSpan.FromSeconds(10)));

            // Named HttpClient for lightweight API endpoint probing (health checks).
            // This avoids creating a new HttpClient per probe call (socket exhaustion).
            builder.Services.AddHttpClient("ProbeClient", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(5);
            });

            // Register Core Architecture Services (TTS, Bookmarks, Caching, Recently Read)
            builder.Services.AddSingleton<NigerianNewGrid.Services.ITextToSpeechService, NigerianNewGrid.Services.MauiTextToSpeechService>();
            builder.Services.AddSingleton<NigerianNewGrid.Services.IBookmarkService, NigerianNewGrid.Services.BookmarkService>();
            builder.Services.AddSingleton<NigerianNewGrid.Services.IRecentlyReadService, NigerianNewGrid.Services.RecentlyReadService>();
            builder.Services.AddSingleton<NigerianNewGrid.Services.IBriefingCacheService, NigerianNewGrid.Services.BriefingCacheService>();
            builder.Services.AddSingleton<NigerianNewGrid.Services.IAnalyticsService, NigerianNewGrid.Services.AnalyticsService>();

            // Register On-Device Notification and Keyword Matching Services
            builder.Services.AddSingleton<NigerianNewGrid.Services.IKeywordMatchingService, NigerianNewGrid.Services.KeywordMatchingService>();
#if ANDROID
            builder.Services.AddSingleton<NigerianNewGrid.Services.INotificationService, Platforms.Android.NotificationService>();
#elif IOS
            builder.Services.AddSingleton<NigerianNewGrid.Services.INotificationService, Platforms.iOS.NotificationService>();
#elif MACCATALYST
            builder.Services.AddSingleton<NigerianNewGrid.Services.INotificationService, Platforms.MacCatalyst.NotificationService>();
#elif WINDOWS
            builder.Services.AddSingleton<NigerianNewGrid.Services.INotificationService, Platforms.Windows.NotificationService>();
#else
            builder.Services.AddSingleton<NigerianNewGrid.Services.INotificationService, NigerianNewGrid.Services.NullNotificationService>();
#endif

            // Register UI ViewModels (Fix #32, #33)
            builder.Services.AddTransient<NigerianNewGrid.ViewModels.MainViewModel>();
            builder.Services.AddTransient<NigerianNewGrid.ViewModels.DiscoverViewModel>();

            // Register UI pages so they can be resolved from DI if needed.
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<ArticleWebPage>();
            builder.Services.AddTransient<BookmarksPage>();
            builder.Services.AddTransient<DiscoverPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<OnboardingPage>();
            builder.Services.AddSingleton<AppShell>();

            var app = builder.Build();
            return app;
        }
    }
}
