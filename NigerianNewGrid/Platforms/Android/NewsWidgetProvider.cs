using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using Android.Net;
using Android.OS;

namespace NigerianNewGrid.Platforms.Android;

[BroadcastReceiver(Name = "com.companyname.nigeriannewgrid.NewsWidgetProvider", Label = "Top News Grid", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate, ActionWidgetRefresh })]
[MetaData("android.appwidget.provider", Resource = "@xml/news_widget_info")]
public class NewsWidgetProvider : AppWidgetProvider
{
    public const string ActionWidgetRefresh = "com.companyname.nigeriannewgrid.ACTION_WIDGET_REFRESH";
    public const string ActionItemClick = "com.companyname.nigeriannewgrid.ACTION_WIDGET_ITEM_CLICK";

    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context == null || appWidgetManager == null || appWidgetIds == null) return;

        foreach (var appWidgetId in appWidgetIds)
        {
            UpdateWidget(context, appWidgetManager, appWidgetId);
        }

        base.OnUpdate(context, appWidgetManager, appWidgetIds);
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        base.OnReceive(context, intent);

        if (context == null || intent == null) return;

        if (string.Equals(intent.Action, ActionWidgetRefresh, StringComparison.OrdinalIgnoreCase))
        {
            var appWidgetManager = AppWidgetManager.GetInstance(context);
            var componentName = new ComponentName(context, "com.companyname.nigeriannewgrid.NewsWidgetProvider");
            var appWidgetIds = appWidgetManager?.GetAppWidgetIds(componentName);

            if (appWidgetManager != null && appWidgetIds != null && appWidgetIds.Length > 0)
            {
                try
                {
                    var notifyMethod = typeof(AppWidgetManager).GetMethods()
                        .FirstOrDefault(m => string.Equals(m.Name, "NotifyAppWidgetDataChanged", StringComparison.OrdinalIgnoreCase));

                    if (notifyMethod != null)
                    {
                        var paramsCount = notifyMethod.GetParameters().Length;
                        if (paramsCount == 2)
                        {
                            var firstParamType = notifyMethod.GetParameters()[0].ParameterType;
                            if (firstParamType == typeof(int[]))
                            {
                                notifyMethod.Invoke(appWidgetManager, new object[] { appWidgetIds, Resource.Id.widget_news_list });
                            }
                            else if (firstParamType == typeof(int))
                            {
                                foreach (var appWidgetId in appWidgetIds)
                                {
                                    notifyMethod.Invoke(appWidgetManager, new object[] { appWidgetId, Resource.Id.widget_news_list });
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NewsWidgetProvider] Notify error: {ex.Message}");
                }

                foreach (var appWidgetId in appWidgetIds)
                {
                    UpdateWidget(context, appWidgetManager, appWidgetId);
                }
            }
        }
        else if (string.Equals(intent.Action, ActionItemClick, StringComparison.OrdinalIgnoreCase))
        {
            var articleUrl = intent.GetStringExtra("article_url");
            var articleId = intent.GetStringExtra("article_id");
            var articleTitle = intent.GetStringExtra("article_title");
            var category = intent.GetStringExtra("article_category");

            var launchIntent = new Intent(context, typeof(MainActivity));
            launchIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            launchIntent.PutExtra("article_url", articleUrl);
            launchIntent.PutExtra("article_id", articleId);
            launchIntent.PutExtra("article_title", articleTitle);
            launchIntent.PutExtra("article_category", category);
            context.StartActivity(launchIntent);
        }
    }

    public static void UpdateWidget(Context context, AppWidgetManager appWidgetManager, int appWidgetId)
    {
        try
        {
            var views = new RemoteViews(context.PackageName, Resource.Layout.widget_news_layout);

            // Bind ListView to RemoteViewsService using explicit Java service name
            var serviceIntent = new Intent(context, typeof(NewsWidgetService));
            serviceIntent.SetComponent(new ComponentName(context, "com.companyname.nigeriannewgrid.NewsWidgetService"));
            serviceIntent.PutExtra(AppWidgetManager.ExtraAppwidgetId, appWidgetId);
            serviceIntent.SetData(global::Android.Net.Uri.Parse(serviceIntent.ToUri(IntentUriType.Scheme)));

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
            if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
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
}
