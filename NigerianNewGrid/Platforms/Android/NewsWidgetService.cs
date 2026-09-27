using Android.App;
using Android.Content;
using Android.Widget;
using System.Text.Json;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;

namespace NigerianNewGrid.Platforms.Android;

[Service(Name = "com.companyname.nigeriannewgrid.NewsWidgetService", Permission = "android.permission.BIND_REMOTEVIEWS", Exported = false)]
public class NewsWidgetService : RemoteViewsService
{
    public override IRemoteViewsFactory? OnGetViewFactory(Intent? intent)
    {
        return new NewsWidgetFactory(ApplicationContext);
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
        try
        {
            var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, string.Empty);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = "http://10.0.2.2:56193";
            }

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var requestUrl = $"{baseUrl.TrimEnd('/')}/api/v1/articles?pageSize=10";
            var response = httpClient.GetAsync(requestUrl).ConfigureAwait(false).GetAwaiter().GetResult();

            if (response.IsSuccessStatusCode)
            {
                var json = response.Content.ReadAsStringAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                var items = JsonSerializer.Deserialize<List<BriefingItem>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (items != null && items.Count > 0)
                {
                    _articles = items.Take(10).ToList();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NewsWidgetFactory] API fetch error: {ex.Message}");
        }

        // Fallback to cached daily briefing
        try
        {
            var cached = Preferences.Get(AppPreferenceKeys.LastBriefing, string.Empty);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                var categories = JsonSerializer.Deserialize<List<BriefingCategory>>(cached, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (categories != null)
                {
                    var cachedItems = categories
                        .SelectMany(c => c.Top)
                        .DistinctBy(i => i.Id)
                        .Take(10)
                        .ToList();

                    if (cachedItems.Count > 0)
                    {
                        _articles = cachedItems;
                        return;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NewsWidgetFactory] Cache fallback error: {ex.Message}");
        }
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

            var source = item.Source ?? "Nigerian News Grid";
            var timeAgo = item.TimeAgoText;
            views.SetTextViewText(Resource.Id.widget_item_meta, $"{source} • {timeAgo}");

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
