namespace NigerianNewGrid;

public partial class AppShell : Shell
{
    public AppShell()
    {
#if DEBUG && ANDROID
        Android.Util.Log.Info("APP_DEBUG", "AppShell constructor START");
#endif
        try
        {
            InitializeComponent();
#if DEBUG && ANDROID
            Android.Util.Log.Info("APP_DEBUG", "AppShell InitializeComponent DONE");
#endif
            Routing.RegisterRoute("article", typeof(ArticleWebPage));
            Routing.RegisterRoute("storycluster", typeof(StoryClusterPage));
#if DEBUG && ANDROID
            Android.Util.Log.Info("APP_DEBUG", "AppShell RegisterRoute DONE");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AppShell constructor ERROR: {ex}");
#if DEBUG && ANDROID
            Android.Util.Log.Error("APP_DEBUG", $"AppShell constructor ERROR: {ex}");
#endif
            throw;
        }
#if DEBUG && ANDROID
        Android.Util.Log.Info("APP_DEBUG", "AppShell constructor FINISHED");
#endif
    }
}
