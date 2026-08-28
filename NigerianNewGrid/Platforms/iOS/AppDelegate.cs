using Foundation;
using UserNotifications;

namespace NigerianNewGrid;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp()
    {
        UNUserNotificationCenter.Current.Delegate = new Platforms.iOS.iOSNotificationDelegate();
        return MauiProgram.CreateMauiApp();
    }
}
