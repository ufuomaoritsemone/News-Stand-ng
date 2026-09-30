using Android.App;
using Android.Content;
using Android.Widget;
using System.Text.Json;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;
using NigerianNewGrid.Services;

namespace NigerianNewGrid.Platforms.Android;

[Service(Name = "com.nigeriannewsgrid.app.NewsWidgetService", Permission = "android.permission.BIND_REMOTEVIEWS", Exported = false)]
public class NewsWidgetService : RemoteViewsService
{
    public override IRemoteViewsFactory? OnGetViewFactory(Intent? intent)
    {
        return new NewsWidgetFactory(ApplicationContext ?? this);
    }
}

public class NewsWidgetFactory : Java.Lang.Object, RemoteViewsService.IRemoteViewsFactory
{
    private readonly Context _context;
    private List<BriefingItem> _articles = new();

    public NewsWidgetFactory(Context context)
    {
        _context = context;
    }

    public void OnCreate()
    {
        FetchNews();
    }

    public void OnDataSetChanged()
    {
        FetchNews();
    }

    private void FetchNews()
    {
        // 1. Immediately populate from local cache so the widget renders instantly without network delay
        LoadCachedNews();

        // If user opted out of background updates to save battery, skip network call if cached articles exist
        if (!NotificationPreferences.BackgroundUpdatesEnabled && _articles.Count > 0)
        {
            return;
        }

        // 2. Refresh from API in the background if network is reachable
        try
        {
            var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, string.Empty);
            if (!string.IsNullOrWhiteSpace(baseUrl) && !baseUrl.Contains("10.0.2.2"))
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var requestUrl = $"{baseUrl.TrimEnd('/')}/api/v1/articles?pageSize=10";
                var response = Task.Run(() => httpClient.GetAsync(requestUrl)).GetAwaiter().GetResult();

                if (response.IsSuccessStatusCode)
                {
                    var json = Task.Run(() => response.Content.ReadAsStringAsync()).GetAwaiter().GetResult();
                    var items = JsonSerializer.Deserialize<List<BriefingItem>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (items?.Count > 0)
                    {
                        _articles = items.Take(10).ToList();
                        PrefetchThumbnails(_articles);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NewsWidgetFactory] API fetch error: {ex.Message}");
        }
    }

    private void LoadCachedNews()
    {
        try
        {
            var persistenceService = IPlatformApplication.Current?.Services.GetService<INewsPersistenceService>()
                ?? new NewsPersistenceService(Microsoft.Extensions.Logging.Abstractions.NullLogger<NewsPersistenceService>.Instance);

            var categories = Task.Run(async () => await persistenceService.GetCachedBriefingAsync(days: 14)).GetAwaiter().GetResult();
            if (categories is { Count: > 0 })
            {
                var cachedItems = categories
                    .SelectMany(c => c.Top)
                    .DistinctBy(i => i.Id)
                    .Take(10)
                    .ToList();

                if (cachedItems.Count > 0)
                {
                    _articles = cachedItems;
                    PrefetchThumbnails(_articles);
                    return;
                }
            }

            // Fallback for legacy Preferences if present
            var cached = Preferences.Get(AppPreferenceKeys.LastBriefing, string.Empty);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                var legacyCategories = JsonSerializer.Deserialize<List<BriefingCategory>>(cached, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (legacyCategories != null)
                {
                    var cachedItems = legacyCategories
                        .SelectMany(c => c.Top)
                        .DistinctBy(i => i.Id)
                        .Take(10)
                        .ToList();

                    if (cachedItems.Count > 0)
                    {
                        _articles = cachedItems;
                        PrefetchThumbnails(_articles);
                        return;
                    }
                }
            }

            // Fallback to sample briefing if cache is empty so widget is NEVER blank
            var fallback = new BriefingCacheService(
                Microsoft.Extensions.Logging.Abstractions.NullLogger<BriefingCacheService>.Instance,
                persistenceService)
                .GetFallbackSampleBriefing("English");

            var fallbackItems = fallback
                .SelectMany(c => c.Top)
                .DistinctBy(i => i.Id)
                .Take(10)
                .ToList();

            if (fallbackItems.Count > 0)
            {
                _articles = fallbackItems;
                Task.Run(async () =>
                {
                    try { await persistenceService.SaveBriefingAsync(fallback); } catch { }
                });
                PrefetchThumbnails(_articles);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NewsWidgetFactory] Cache load error: {ex.Message}");
        }
    }

    private void PrefetchThumbnails(List<BriefingItem> articles)
    {
        if (articles == null || articles.Count == 0) return;
        var toPrefetch = articles.Take(8).ToList();
        Task.Run(() =>
        {
            foreach (var art in toPrefetch)
            {
                if (!string.IsNullOrWhiteSpace(art.ImageUrl))
                {
                    WidgetImageHelper.GetOrDownloadThumbnailBitmap(_context, art.ImageUrl, 77);
                }
            }
        });
    }

    public void OnDestroy()
    {
        _articles.Clear();
    }

    public int Count => _articles.Count;

    public RemoteViews? GetViewAt(int position)
    {
        if (position < 0 || position >= _articles.Count)
            return null;

        try
        {
            var item = _articles[position];
            var views = new RemoteViews(_context.PackageName, Resource.Layout.widget_news_item);

            var categoryText = (item.Category ?? "NEWS").ToUpperInvariant();
            views.SetTextViewText(Resource.Id.widget_item_category, categoryText);
            views.SetTextViewText(Resource.Id.widget_item_title, item.Title ?? "No Title");

            var source = item.Source ?? "News Stand NG";
            var timeAgo = item.TimeAgoText;
            views.SetTextViewText(Resource.Id.widget_item_meta, $"{source} • {timeAgo}");

            // Bind thumbnail image if available, else collapse ImageView so text expands smoothly
            if (!string.IsNullOrWhiteSpace(item.ImageUrl))
            {
                var thumbnail = WidgetImageHelper.GetOrDownloadThumbnailBitmap(_context, item.ImageUrl, 77);
                if (thumbnail != null && !thumbnail.IsRecycled)
                {
                    views.SetImageViewBitmap(Resource.Id.widget_item_image, thumbnail);
                    views.SetViewVisibility(Resource.Id.widget_item_image, global::Android.Views.ViewStates.Visible);
                }
                else
                {
                    views.SetViewVisibility(Resource.Id.widget_item_image, global::Android.Views.ViewStates.Gone);
                }
            }
            else
            {
                views.SetViewVisibility(Resource.Id.widget_item_image, global::Android.Views.ViewStates.Gone);
            }

            var fillInIntent = new Intent();
            fillInIntent.PutExtra("article_url", item.Url ?? string.Empty);
            fillInIntent.PutExtra("article_id", item.Id ?? string.Empty);
            fillInIntent.PutExtra("article_title", item.Title ?? string.Empty);
            fillInIntent.PutExtra("article_category", item.Category ?? string.Empty);
            views.SetOnClickFillInIntent(Resource.Id.widget_item_container, fillInIntent);

            return views;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NewsWidgetFactory] GetViewAt error: {ex.Message}");
            return null;
        }
    }

    public RemoteViews? LoadingView => null;

    public int ViewTypeCount => 1;

    public long GetItemId(int position)
    {
        return position;
    }

    public bool HasStableIds => true;
}
