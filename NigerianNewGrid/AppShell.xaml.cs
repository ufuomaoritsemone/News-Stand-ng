namespace NigerianNewGrid;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register modal routes for in-app article reader
        // These are pushed on top of the tab bar (no tab navigation)
        Routing.RegisterRoute("article", typeof(ArticleWebPage));
    }
}
