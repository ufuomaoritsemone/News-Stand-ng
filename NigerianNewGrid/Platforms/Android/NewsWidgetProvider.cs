using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using Android.Net;
using Android.OS;

namespace NigerianNewGrid.Platforms.Android;

[BroadcastReceiver(Name = "com.nigeriannewsgrid.app.NewsWidgetProvider", Label = "News Stand NG", Exported = true)]
[IntentFilter([AppWidgetManager.ActionAppwidgetUpdate, ActionWidgetRefresh])]
[MetaData("android.appwidget.provider", Resource = "@xml/news_widget_info")]
public class NewsWidgetProvider : AppWidgetProvider
{
    public const string ActionWidgetRefresh = "com.nigeriannewsgrid.app.ACTION_WIDGET_REFRESH";
    public const string ActionItemClick = "com.nigeriannewsgrid.app.ACTION_WIDGET_ITEM_CLICK";

    public override void OnEnabled(Context? context)
    {
        // Fired once when the FIRST widget instance is added to the home screen.
        // This is the most important metric: how many users deploy the widget.
        base.OnEnabled(context);
        if (context != null)
            WidgetAnalyticsHelper.Track(context, "widget_enabled");
    }

    public override void OnDisabled(Context? context)
    {
        // Fired once when the LAST widget instance is removed from the home screen.
        base.OnDisabled(context);
        if (context != null)
            WidgetAnalyticsHelper.Track(context, "widget_disabled");
    }

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context == null || appWidgetManager == null || appWidgetIds == null) return;

        foreach (var appWidgetId in appWidgetIds)
        {
            UpdateWidget(context, appWidgetManager, appWidgetId);
        }

        // Ensure collection items are notified to re-query the factory on interval
        NotifyDataChanged(appWidgetManager, appWidgetIds);

        base.OnUpdate(context, appWidgetManager, appWidgetIds);
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        base.OnReceive(context, intent);

        if (context == null || intent == null) return;

        if (string.Equals(intent.Action, ActionWidgetRefresh, StringComparison.OrdinalIgnoreCase))
        {
            // Track every user-initiated widget refresh
            WidgetAnalyticsHelper.Track(context, "widget_refresh");

            var appWidgetManager = AppWidgetManager.GetInstance(context);
            var componentName = new ComponentName(context, Java.Lang.Class.FromType(typeof(NewsWidgetProvider)));
            var appWidgetIds = appWidgetManager?.GetAppWidgetIds(componentName);

            if (appWidgetManager != null && appWidgetIds != null && appWidgetIds.Length > 0)
            {
                foreach (var appWidgetId in appWidgetIds)
                {
                    UpdateWidget(context, appWidgetManager, appWidgetId);
                }

                NotifyDataChanged(appWidgetManager, appWidgetIds);
            }
        }
        else if (string.Equals(intent.Action, ActionItemClick, StringComparison.OrdinalIgnoreCase))
        {
            var articleUrl   = intent.GetStringExtra("article_url");
            var articleId    = intent.GetStringExtra("article_id");
            var articleTitle = intent.GetStringExtra("article_title");
            var category     = intent.GetStringExtra("article_category");

            // Track widget story tap — same device_id as in-app events for cross-surface attribution
            WidgetAnalyticsHelper.Track(
                context,
                "widget_item_click",
                articleId:    articleId,
                articleTitle: articleTitle,
                category:     category);

            var launchIntent = new Intent(context, typeof(MainActivity));
            launchIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            launchIntent.PutExtra("article_url",      articleUrl);
            launchIntent.PutExtra("article_id",       articleId);
            launchIntent.PutExtra("article_title",    articleTitle);
            launchIntent.PutExtra("article_category", category);
            context.StartActivity(launchIntent);
        }
    }

    public static void UpdateWidget(Context context, AppWidgetManager appWidgetManager, int appWidgetId)
    {
        try
        {
            var views = new RemoteViews(context.PackageName, Resource.Layout.widget_news_layout);

            // Bind ListView to RemoteViewsService
            var serviceIntent = new Intent(context, typeof(NewsWidgetService));
            serviceIntent.PutExtra(AppWidgetManager.ExtraAppwidgetId, appWidgetId);
            serviceIntent.SetData(global::Android.Net.Uri.FromParts("content", appWidgetId.ToString(), null));

#pragma warning disable CA1422 // RemoteViews.SetRemoteAdapter(int, Intent) and NotifyAppWidgetViewDataChanged obsoleted in Android 35 but required for < 35
            views.SetRemoteAdapter(Resource.Id.widget_news_list, serviceIntent);
            views.SetEmptyView(Resource.Id.widget_news_list, Resource.Id.widget_empty_view);

            // PendingIntent for refresh button (Immutable)
            var refreshFlags = PendingIntentFlags.UpdateCurrent;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                refreshFlags |= PendingIntentFlags.Immutable;
            }

            var refreshIntent = new Intent(context, typeof(NewsWidgetProvider));
            refreshIntent.SetAction(ActionWidgetRefresh);
            var refreshPendingIntent = PendingIntent.GetBroadcast(
                context,
                0,
                refreshIntent,
                refreshFlags);
            views.SetOnClickPendingIntent(Resource.Id.widget_refresh_btn, refreshPendingIntent);

            // PendingIntent template for list item clicks (Mutable on Android 12+)
            var clickFlags = PendingIntentFlags.UpdateCurrent;
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                clickFlags |= PendingIntentFlags.Mutable;
            }

            var itemClickIntent = new Intent(context, typeof(NewsWidgetProvider));
            itemClickIntent.SetAction(ActionItemClick);
            var itemClickPendingIntent = PendingIntent.GetBroadcast(
                context,
                0,
                itemClickIntent,
                clickFlags);
            views.SetPendingIntentTemplate(Resource.Id.widget_news_list, itemClickPendingIntent);

            // Update timestamp text
            var now = DateTime.Now.ToString("HH:mm");
            views.SetTextViewText(Resource.Id.widget_updated_time, $"• {now}");

            appWidgetManager.UpdateAppWidget(appWidgetId, views);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NewsWidgetProvider] UpdateWidget error: {ex.Message}");
        }
    }

    public static void NotifyDataChanged(AppWidgetManager appWidgetManager, int[] appWidgetIds)
    {
        if (appWidgetIds == null || appWidgetIds.Length == 0) return;

        try
        {
            appWidgetManager.NotifyAppWidgetViewDataChanged(appWidgetIds, Resource.Id.widget_news_list);
#pragma warning restore CA1422
            System.Diagnostics.Debug.WriteLine($"[NewsWidgetProvider] Notified widget data changed for {appWidgetIds.Length} widget(s).");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NewsWidgetProvider] Notify error: {ex.Message}");
        }
    }
}
