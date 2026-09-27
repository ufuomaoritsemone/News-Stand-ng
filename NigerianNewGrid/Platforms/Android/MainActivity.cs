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
        base.OnCreate(savedInstanceState);
        HandleNotificationIntent(Intent);
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
                        var url = articleUrl;
                        if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(articleId))
                        {
                            url = ResolveUrlFromCache(articleId);
                        }

                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            await Shell.Current.Navigation.PushAsync(
                                new ArticleWebPage(url, articleTitle, category: category, articleId: articleId));
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainActivity] Notification navigation error: {ex.Message}");
                }
            });
        }
    }

    private static string? ResolveUrlFromCache(string articleId)
    {
        try
        {
            var cached = Preferences.Get("last_briefing", string.Empty);
            if (string.IsNullOrWhiteSpace(cached)) return null;

            var categories = JsonSerializer.Deserialize<List<BriefingCategory>>(cached, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            var item = categories?.SelectMany(c => c.Top).FirstOrDefault(i => string.Equals(i.Id, articleId, StringComparison.OrdinalIgnoreCase));
            return item?.Url;
        }
        catch
        {
            return null;
        }
    }
}
