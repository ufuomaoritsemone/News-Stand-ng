using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using System.Text.Json;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Services;

namespace NigerianNewGrid;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Android.Util.Log.Info("APP_DEBUG", "MainActivity.OnCreate START");
        base.OnCreate(savedInstanceState);
        Android.Util.Log.Info("APP_DEBUG", "MainActivity.OnCreate base.OnCreate DONE");
        HandleNotificationIntent(Intent);
        Android.Util.Log.Info("APP_DEBUG", "MainActivity.OnCreate FINISHED");
    }

    protected override void OnResume()
    {
        Android.Util.Log.Info("APP_DEBUG", "MainActivity.OnResume START");
        base.OnResume();
        Android.Util.Log.Info("APP_DEBUG", "MainActivity.OnResume DONE");
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleNotificationIntent(intent);
    }

    private static void HandleNotificationIntent(Intent? intent)
    {
        if (intent == null) return;

        var action = intent.GetStringExtra("action");
        if (string.Equals(action, "play_audio_briefing", StringComparison.OrdinalIgnoreCase))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    int attempts = 0;
                    while (Shell.Current == null && attempts < 15)
                    {
                        await Task.Delay(200);
                        attempts++;
                    }

                    if (Shell.Current != null)
                    {
                        await Shell.Current.GoToAsync("//MainPage");
                        AppNotificationBridge.TriggerAutoPlayAudioBriefing();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainActivity] Audio briefing launch error: {ex.Message}");
                }
            });
            return;
        }

        var articleId = intent.GetStringExtra("article_id");
        var articleUrl = intent.GetStringExtra("article_url");
        var articleTitle = intent.GetStringExtra("article_title");
        var category = intent.GetStringExtra("article_category");

        if (!string.IsNullOrWhiteSpace(articleUrl) || !string.IsNullOrWhiteSpace(articleId))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    int attempts = 0;
                    while (Shell.Current == null && attempts < 15)
                    {
                        await Task.Delay(200);
                        attempts++;
                    }

                    if (Shell.Current != null)
                    {
                        await OpenArticleFromIntentAsync(articleId, articleUrl, articleTitle, category);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainActivity] Notification navigation error: {ex.Message}");
                }
            });
        }
    }

    private static async Task OpenArticleFromIntentAsync(
        string? articleId,
        string? articleUrl,
        string? articleTitle,
        string? category)
    {
        if (Shell.Current == null) return;

        // 1. Try resolving full story by GUID from local SQLite persistence (sub-10ms, offline capable)
        if (!string.IsNullOrWhiteSpace(articleId))
        {
            try
            {
                var persistenceService = IPlatformApplication.Current?.Services.GetService<INewsPersistenceService>();
                if (persistenceService != null)
                {
                    var story = await persistenceService.GetStoryByIdAsync(articleId);
                    if (story != null && !string.IsNullOrWhiteSpace(story.Content))
                    {
                        await Shell.Current.Navigation.PushAsync(new ArticleWebPage(story));
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainActivity] Cache lookup error: {ex.Message}");
            }
        }

        // 2. Resolve URL if missing and launch ArticleWebPage with GUID for automatic hydration
        var url = articleUrl;
        if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(articleId))
        {
            url = await ResolveUrlFromCacheAsync(articleId);
        }

        await Shell.Current.Navigation.PushAsync(
            new ArticleWebPage(
                url: url ?? string.Empty,
                title: articleTitle,
                category: category,
                articleId: articleId));
    }

    private static async Task<string?> ResolveUrlFromCacheAsync(string articleId)
    {
        try
        {
            var persistenceService = IPlatformApplication.Current?.Services.GetService<INewsPersistenceService>();
            if (persistenceService != null)
            {
                var categories = await persistenceService.GetCachedBriefingAsync(days: 14);
                var item = categories?.SelectMany(c => c.Top).FirstOrDefault(i => string.Equals(i.Id, articleId, StringComparison.OrdinalIgnoreCase));
                if (item?.Url != null) return item.Url;
            }

            var cached = Preferences.Get("last_briefing", string.Empty);
            if (string.IsNullOrWhiteSpace(cached)) return null;

            var legacyCategories = JsonSerializer.Deserialize<List<BriefingCategory>>(cached, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var legacyItem = legacyCategories?.SelectMany(c => c.Top).FirstOrDefault(i => string.Equals(i.Id, articleId, StringComparison.OrdinalIgnoreCase));
            return legacyItem?.Url;
        }
        catch
        {
            return null;
        }
    }
}
