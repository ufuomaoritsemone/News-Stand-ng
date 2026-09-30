using System.Diagnostics;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;
using NigerianNewGrid.ViewModels;

namespace NigerianNewGrid;

/// <summary>
/// Main landing page for News Stand NG.
/// Follows MVVM pattern with minimal code-behind: all business logic, data retrieval,
/// pagination, and state are encapsulated in <see cref="MainViewModel"/>.
/// This code-behind is strictly responsible for view-layer concerns (navigation, system share, onboarding dialog).
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly MainViewModel _vm;
    private const string AppShareTagline =
        "\n\n📲 Follow news as it breaks — download News Stand NG and never miss a story.";

    public MainPage(MainViewModel viewModel)
    {
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", "MainPage constructor START");
#endif
        try
        {
            InitializeComponent();
#if ANDROID
            Android.Util.Log.Info("APP_DEBUG", "MainPage InitializeComponent DONE");
#endif
            _vm = viewModel;
            BindingContext = _vm;
            _vm.ScrollToTopRequested += OnScrollToTopRequested;
#if ANDROID
            Android.Util.Log.Info("APP_DEBUG", "MainPage BindingContext SET");
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainPage constructor ERROR: {ex}");
#if ANDROID
            Android.Util.Log.Error("APP_DEBUG", $"MainPage constructor ERROR: {ex}");
#endif
            throw;
        }

        Appearing += async (_, _) =>
        {
#if ANDROID
            Android.Util.Log.Info("APP_DEBUG", "MainPage Appearing FIRED");
#endif
            _vm.OnAppResumed();
            try
            {
                await _vm.InitializeAsync();
#if ANDROID
                Android.Util.Log.Info("APP_DEBUG", "MainPage _vm.InitializeAsync COMPLETED");
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainPage Appearing ERROR: {ex}");
#if ANDROID
                Android.Util.Log.Error("APP_DEBUG", $"MainPage Appearing ERROR: {ex}");
#endif
            }
        };
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", "MainPage constructor FINISHED");
#endif
    }

    private async void OnArticleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not BriefingItem item) return;
        if (item.IsAdMobPlaceholder) return;

        if (item.IsSponsored)
        {
            _vm.TrackArticleClick(item.Id);
            var targetUrl = !string.IsNullOrWhiteSpace(item.SponsorUrl) ? item.SponsorUrl : item.Url;
            if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out _)) return;
            try
            {
                await Navigation.PushAsync(new ArticleWebPage(
                    targetUrl,
                    item.Title,
                    item.ImageUrl,
                    item.Category,
                    item.Id,
                    isSponsored: true,
                    sponsorName: item.SponsorName,
                    content: item.Content,
                    summary: item.Summary));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainPage] Sponsored navigation error: {ex.Message}");
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(item.Url) || !Uri.TryCreate(item.Url, UriKind.Absolute, out _)) return;
        try
        {
            await Navigation.PushAsync(new ArticleWebPage(item));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainPage] Article navigation error: {ex.Message}");
        }
    }

    private async void OnVideoStoryTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not VideoStoryItem story) return;
        var targetUrl = !string.IsNullOrWhiteSpace(story.VideoUrl)
            ? story.VideoUrl
            : $"https://www.youtube.com/watch?v={story.VideoId}";

        try
        {
            await Navigation.PushAsync(new ArticleWebPage(targetUrl, story.Title, story.DisplayThumbnailUrl, "Video", story.Id));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainPage] Video navigation error: {ex.Message}");
        }
    }

    private async void OnSearchToolbarClicked(object? sender, EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync("//discover");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainPage] Search navigation error: {ex.Message}");
        }
    }

    private void OnBookmarkClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: BriefingItem item })
        {
            _vm.ToggleBookmark(item);
        }
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        try
        {
            BriefingItem? item = null;
            if (sender is Button { CommandParameter: BriefingItem btnItem }) item = btnItem;
            else if (sender is BindableObject { BindingContext: BriefingItem bItem }) item = bItem;
            if (item is null) return;

            var shareText = $"Check out this story from {item.Source ?? "News Stand NG"}:\n\n{item.Title}\n\n{item.Summary}\n\nRead more: {item.Url}{AppShareTagline}".Trim();
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "Share News Story",
                Text  = shareText,
                Uri   = !string.IsNullOrWhiteSpace(item.Url) ? item.Url : null
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainPage] Share error: {ex.Message}");
        }
    }

    private void OnScrollToTopRequested()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await FeedScrollView.ScrollToAsync(0, 0, animated: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainPage] ScrollToTop failed: {ex.Message}");
            }
        });
    }
}
