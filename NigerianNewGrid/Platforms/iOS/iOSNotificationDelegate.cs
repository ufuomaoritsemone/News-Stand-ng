using Foundation;
using UserNotifications;

namespace NigerianNewGrid.Platforms.iOS;

public class iOSNotificationDelegate : UNUserNotificationCenterDelegate
{
    [Export("userNotificationCenter:didReceiveNotificationResponse:withCompletionHandler:")]
    public override void DidReceiveNotificationResponse(UNUserNotificationCenter center, UNNotificationResponse response, Action completionHandler)
    {
        var userInfo = response.Notification.Request.Content.UserInfo;
        if (userInfo != null)
        {
            var articleId = userInfo.ValueForKey(new NSString("article_id"))?.ToString();
            var articleUrl = userInfo.ValueForKey(new NSString("article_url"))?.ToString();
            var articleTitle = userInfo.ValueForKey(new NSString("article_title"))?.ToString();
            var category = userInfo.ValueForKey(new NSString("article_category"))?.ToString();

            if (!string.IsNullOrWhiteSpace(articleUrl) || !string.IsNullOrWhiteSpace(articleId))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        if (Shell.Current != null && !string.IsNullOrWhiteSpace(articleUrl))
                        {
                            await Shell.Current.Navigation.PushAsync(
                                new ArticleWebPage(articleUrl, articleTitle, category: category, articleId: articleId));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[iOSNotificationDelegate] Navigation error: {ex.Message}");
                    }
                });
            }
        }

        completionHandler();
    }

    [Export("userNotificationCenter:willPresentNotification:withCompletionHandler:")]
    public override void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
    {
        // Show banner and sound when app is in foreground
        completionHandler(OperatingSystem.IsIOSVersionAtLeast(14)
            ? UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.Sound
            : UNNotificationPresentationOptions.Alert | UNNotificationPresentationOptions.Sound);
    }
}
