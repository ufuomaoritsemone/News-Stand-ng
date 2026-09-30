using System.Diagnostics;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Services;
using NigerianNewGrid.ViewModels;

namespace NigerianNewGrid;

/// <summary>
/// Discover tab — search + category filter over 7-day articles and trending YouTube video stories,
/// with full MVVM data binding, compiled XAML data templates, and infinite scroll (Fix #13).
/// </summary>
public partial class DiscoverPage : ContentPage
{
    private readonly DiscoverViewModel _vm;
    private readonly IRecentlyReadService _recentlyReadService;
    private bool _isInitialized;

    public DiscoverPage(
        DiscoverViewModel viewModel,
        IRecentlyReadService recentlyReadService)
    {
        InitializeComponent();
        _vm                  = viewModel;
        _recentlyReadService = recentlyReadService;
        BindingContext       = _vm;

        Appearing += async (_, _) =>
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                await _vm.LoadAsync();
            }
            else if (string.Equals(_vm.SelectedCategory, "RecentlyRead", StringComparison.OrdinalIgnoreCase))
            {
                await _vm.ApplyCategoryFilterAsync("RecentlyRead");
            }
        };
    }

    private async void OnArticleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not BriefingItem item) return;
        _recentlyReadService.RecordRead(item);

        if (item.IsSponsored && !string.IsNullOrWhiteSpace(item.SponsorUrl))
        {
            var targetUrl = item.SponsorUrl.Trim();
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
                Debug.WriteLine($"[DiscoverPage] Sponsored navigation error: {ex.Message}");
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
            Debug.WriteLine($"[DiscoverPage] Article navigation error: {ex.Message}");
        }
    }

    private async void OnVideoTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not VideoStoryItem video) return;
        var targetUrl = !string.IsNullOrWhiteSpace(video.VideoUrl)
            ? video.VideoUrl
            : $"https://www.youtube.com/watch?v={video.VideoId}";

        if (!string.IsNullOrWhiteSpace(targetUrl))
        {
            try
            {
                await Navigation.PushAsync(new ArticleWebPage(targetUrl, video.Title, video.DisplayThumbnailUrl, video.Category, video.Id));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DiscoverPage] Video navigation error: {ex.Message}");
            }
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

            var shareText = $"Check out this story from {item.Source ?? "News Stand NG"}:\n\n{item.Title}\n\n{item.Summary}\n\nRead more: {item.Url}\n\nShared via News Stand NG".Trim();
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "Share News Story",
                Text  = shareText,
                Uri   = !string.IsNullOrWhiteSpace(item.Url) ? item.Url : null
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DiscoverPage] Share error: {ex.Message}");
        }
    }

    private async void OnSearchClicked(object? sender, EventArgs e)
    {
        await _vm.SearchAsync(DiscoverSearchEntry?.Text);
    }

    private async void OnClearSearch(object? sender, EventArgs e)
    {
        DiscoverSearchEntry.Text = string.Empty;
        await _vm.ClearSearchAsync();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _vm.IsClearButtonVisible = !string.IsNullOrWhiteSpace(e.NewTextValue);
    }

    private async void OnLoadMoreClicked(object? sender, EventArgs e)
    {
        await _vm.LoadMoreAsync();
    }

    private async void OnScrollViewScrolled(object? sender, ScrolledEventArgs e)
    {
        if (DiscoverScrollView == null || _vm.IsLoadingMore || !_vm.IsLoadMoreVisible) return;

        if (DiscoverScrollView.ContentSize.Height > 0)
        {
            var currentPos = e.ScrollY + DiscoverScrollView.Height;
            var threshold = DiscoverScrollView.ContentSize.Height * 0.85;

            if (currentPos >= threshold)
            {
                await _vm.LoadMoreAsync();
            }
        }
    }

    private async void OnCategoryPillTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string cat) return;
        if (string.Equals(_vm.SelectedCategory, cat, StringComparison.OrdinalIgnoreCase) && !_vm.IsSearchMode) return;

        var pills  = new[] { PillAll, PillRecentlyRead, PillKeywordAlerts, PillOpinions, PillPolitics, PillSports, PillBusiness, PillEntertainment, PillTech, PillInternational };
        var labels = new[] { "All", "RecentlyRead", "KeywordAlerts", "Opinion", "Politics", "Sports", "Business", "Entertainment", "Technology", "International" };

        for (int i = 0; i < pills.Length; i++)
        {
            bool isSelected = string.Equals(labels[i], cat, StringComparison.OrdinalIgnoreCase) ||
                              (cat == "All" && labels[i] == "All");

            if (isSelected)
            {
                pills[i].BackgroundColor = GetRes<Color>("AccentBlue", Color.FromArgb("#0066FF"));
                pills[i].StrokeThickness = 0;
                if (pills[i].Content is Label lbl)
                {
                    lbl.TextColor = Colors.White;
                    lbl.FontAttributes = FontAttributes.Bold;
                }
            }
            else
            {
                pills[i].BackgroundColor = GetRes<Color>("White", Colors.White);
                pills[i].Stroke = GetRes<Brush>("Gray200Brush", new SolidColorBrush(Color.FromArgb("#D5E2D8")));
                pills[i].StrokeThickness = 1;
                if (pills[i].Content is Label lbl)
                {
                    lbl.TextColor = GetRes<Color>("Gray900", Color.FromArgb("#212529"));
                    lbl.FontAttributes = FontAttributes.Bold;
                }
            }
        }

        await _vm.ApplyCategoryFilterAsync(cat);
    }

    private static T GetRes<T>(string key, T fallback)
    {
        if (Application.Current?.Resources is { } res &&
            res.TryGetValue(key, out var raw) && raw is T typed)
            return typed;
        return fallback;
    }
}
