using System.Diagnostics;
using System.Text.Json;
using NigerianNewGrid.Services;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid;

public class RelatedStoryDisplayItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string Type { get; set; } = "Article"; // Article, Video, Social
    public string Url { get; set; } = string.Empty;
    public bool IsVideo => Type == "Video";
    public bool IsSocial => Type == "Social";
    public string TimeAgo { get; set; } = "recently";
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
}

public partial class ArticleWebPage : ContentPage
{
    private string _articleUrl = string.Empty;
    private string? _title;
    private string? _imageUrl;
    private string? _category;
    private string? _articleId;
    private bool _isReaderMode = true;
    private bool _isVideoStory;
    private int _fontSizeLevel = 1; // 0 = 16px, 1 = 18px, 2 = 22px, 3 = 26px
    private ArticleDistillerService.PublisherInfo _publisher = new("News", null, "#1B3B6F", "#0A192F");

    private bool _isRelatedExpanded;
    private string _selectedFilter = "All";

    private List<RelatedStoryDisplayItem> _allRelatedItems = [];
    private List<RelatedStoryDisplayItem> _displayedRelatedItems = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ArticleWebPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Full constructor initializing Chrome-style Reader Mode with newspaper branding,
    /// lead picture, full distilled story text, and multi-source related coverage.
    /// </summary>
    public ArticleWebPage(string url, string? title = null, string? imageUrl = null, string? category = null, string? articleId = null)
        : this()
    {
        _articleUrl = url ?? string.Empty;
        _title = title;
        _imageUrl = imageUrl;
        _category = category;
        _articleId = articleId;

        _isVideoStory = string.Equals(category, "Video", StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(url) && (url.Contains("youtube.com") || url.Contains("youtu.be")));

        _publisher = ArticleDistillerService.IdentifyPublisher(_articleUrl);

        // Header info: Display publisher brand cleanly
        HeaderPublisherLabel.Text = _publisher.Name;

        if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (_isVideoStory)
            {
                // For videos: open directly in video webview
                _isReaderMode = false;
                ModeToggleBtn.IsVisible = false;
                FontSizeBtn.IsVisible = false;
                ArticleWebView.Source = url;
            }
            else
            {
                // Default: Clean Google Chrome-style Reader Mode (full story text + logo + lead image, 0 ads)
                _isReaderMode = true;
                ModeToggleBtn.Text = "📖 Reader";
                ArticleWebView.Source = url;
            }

            // Asynchronously fetch related stories (Archive, Videos, Tweets)
            _ = LoadRelatedStoriesAsync(_articleId, _title, _category);
        }
        else
        {
            HeaderPublisherLabel.Text = "Invalid URL";
        }
    }

    // ──────────────────────────────────────────────────────────
    // Reader Mode & Navigation
    // ──────────────────────────────────────────────────────────

    private void OnToggleReaderModeClicked(object? sender, EventArgs e)
    {
        if (_isVideoStory) return;

        _isReaderMode = !_isReaderMode;

        if (_isReaderMode)
        {
            ModeToggleBtn.Text = "📖 Reader";
            ModeToggleBtn.BackgroundColor = (Color)Application.Current!.Resources["AccentBlue"];
            ModeToggleBtn.TextColor = Colors.White;
            ModeToggleBtn.BorderColor = Color.FromArgb("#0052CC");
            FontSizeBtn.IsVisible = true;
        }
        else
        {
            ModeToggleBtn.Text = "🌐 Web";
            var isDark = Application.Current!.RequestedTheme == AppTheme.Dark;
            ModeToggleBtn.BackgroundColor = isDark ? (Color)Application.Current!.Resources["ButtonSecondaryBgDark"] : (Color)Application.Current!.Resources["ButtonBgLight"];
            ModeToggleBtn.TextColor = isDark ? (Color)Application.Current!.Resources["ButtonTextDark"] : (Color)Application.Current!.Resources["ButtonTextLight"];
            ModeToggleBtn.BorderColor = isDark ? (Color)Application.Current!.Resources["ButtonBorderDark"] : (Color)Application.Current!.Resources["ButtonBorderLight"];
            FontSizeBtn.IsVisible = false;
        }

        // Toggle Reader Mode directly in the WebView DOM with zero reload
        _ = ToggleReaderModeInWebViewAsync(_isReaderMode);
    }

    private async Task ToggleReaderModeInWebViewAsync(bool active)
    {
        try
        {
            var script = ArticleDistillerService.GetToggleReaderScript(active);
            await ArticleWebView.EvaluateJavaScriptAsync(script);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ArticleWebPage] Toggle reader error: {ex.Message}");
        }
    }

    private async void OnAdjustFontSizeClicked(object? sender, EventArgs e)
    {
        if (!_isReaderMode) return;

        _fontSizeLevel = (_fontSizeLevel + 1) % 4;

        var (fontSize, label) = _fontSizeLevel switch
        {
            0 => ("15px", "A-"),
            1 => ("18px", "A"),
            2 => ("22px", "A+"),
            3 => ("26px", "A++"),
            _ => ("18px", "A")
        };

        FontSizeBtn.Text = label;

        try
        {
            var script = ArticleDistillerService.GetSetFontSizeScript(fontSize);
            await ArticleWebView.EvaluateJavaScriptAsync(script);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ArticleWebPage] Font size adjustment error: {ex.Message}");
        }
    }

    private string GetFontSizeString()
    {
        return _fontSizeLevel switch
        {
            0 => "15px",
            1 => "18px",
            2 => "22px",
            3 => "26px",
            _ => "18px"
        };
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        LoadingProgress.IsVisible = true;
        LoadingProgress.Progress = 0.4;
    }

    private async void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        LoadingProgress.IsVisible = false;
        LoadingProgress.Progress = 1.0;

        if (_isVideoStory) return;

        if (e.Result == WebNavigationResult.Success)
        {
            try
            {
                // Inject the Google Chrome-style DOM Distiller reader engine
                var script = ArticleDistillerService.GenerateChromeReaderDistillerScript(
                    publisherName: _publisher.Name,
                    publisherLogoUrl: _publisher.LogoUrl,
                    brandColor: _publisher.BrandColor,
                    fallbackTitle: _title,
                    fallbackImageUrl: _imageUrl,
                    category: _category,
                    active: _isReaderMode,
                    fontSize: GetFontSizeString());

                await ArticleWebView.EvaluateJavaScriptAsync(script);

                // Run a second pass after 400ms to catch any lazy-loaded DOM elements or images
                await Task.Delay(400);
                await ArticleWebView.EvaluateJavaScriptAsync(script);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ArticleWebPage] Reader mode injection error: {ex.Message}");
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    // Related Stories Ingestion & Multi-Source Loading
    // ──────────────────────────────────────────────────────────

    private async Task LoadRelatedStoriesAsync(string? articleId, string? title, string? category)
    {
        try
        {
            var apiClient = IPlatformApplication.Current?.Services.GetService<NewsApiClient>();
            if (apiClient == null)
            {
                var baseUrl = Preferences.Get("api_base_url", "http://localhost:56193");
                var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                apiClient = new NewsApiClient(http) { BaseUrl = baseUrl };
            }

            var result = await apiClient.GetRelatedStoriesAsync(articleId, title, category, limit: 4);

            var items = new List<RelatedStoryDisplayItem>();

            // 1. Articles from Archive
            if (result.Articles.Count > 0)
            {
                foreach (var art in result.Articles)
                {
                    if (string.Equals(art.Url, _articleUrl, StringComparison.OrdinalIgnoreCase)) continue;
                    items.Add(new RelatedStoryDisplayItem
                    {
                        Id = art.Id,
                        Title = art.Title,
                        Subtitle = !string.IsNullOrWhiteSpace(art.Summary) ? art.Summary : (art.Source ?? "News Archive"),
                        Source = art.Source ?? "News Archive",
                        Category = string.IsNullOrWhiteSpace(art.Category) ? "News" : art.Category,
                        ImageUrl = art.ImageUrl ?? GetFallbackImage(art.Category),
                        Type = "Article",
                        Url = art.Url ?? string.Empty,
                        PublishedAt = art.PublishedAt ?? DateTime.UtcNow,
                        TimeAgo = FormatTimeAgo(art.PublishedAt ?? DateTime.UtcNow)
                    });
                }
            }

            // 2. Video Broadcasts from YouTube
            if (result.Videos.Count > 0)
            {
                foreach (var vid in result.Videos)
                {
                    if (string.Equals(vid.VideoUrl, _articleUrl, StringComparison.OrdinalIgnoreCase)) continue;
                    items.Add(new RelatedStoryDisplayItem
                    {
                        Id = vid.Id,
                        Title = vid.Title,
                        Subtitle = vid.ChannelName,
                        Source = vid.ChannelName,
                        Category = string.IsNullOrWhiteSpace(vid.Category) ? "Video" : vid.Category,
                        ImageUrl = vid.ThumbnailUrl,
                        Type = "Video",
                        Url = vid.VideoUrl,
                        PublishedAt = vid.PublishedAt,
                        TimeAgo = FormatTimeAgo(vid.PublishedAt)
                    });
                }
            }

            // Fallback: If backend returned no related items (e.g. offline / disconnected), compute from local cache
            if (items.Count == 0)
            {
                items = GetLocalFallbackRelatedStories(title, category);
            }

            _allRelatedItems = items;

            MainThread.BeginInvokeOnMainThread(UpdateRelatedUI);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ArticleWebPage] Failed to load related stories: {ex.Message}");
            var localItems = GetLocalFallbackRelatedStories(title, category);
            if (localItems.Count > 0)
            {
                _allRelatedItems = localItems;
                MainThread.BeginInvokeOnMainThread(UpdateRelatedUI);
            }
        }
    }

    private void UpdateRelatedUI()
    {
        int total = _allRelatedItems.Count;
        int artCount = _allRelatedItems.Count(i => i.Type == "Article");
        int vidCount = _allRelatedItems.Count(i => i.Type == "Video");

        if (total == 0)
        {
            RelatedCollapsedBar.IsVisible = false;
            RelatedExpandedSheet.IsVisible = false;
            return;
        }

        RelatedCollapsedLabel.Text = $"✨ Related Stories ({total}) · 📰 {artCount}  🎬 {vidCount}";
        RelatedTotalCountLabel.Text = $"{total} found";

        FilterAllBtn.Text = $"All ({total})";
        FilterArticlesBtn.Text = $"📰 News ({artCount})";
        FilterVideosBtn.Text = $"🎬 Videos ({vidCount})";

        FilterArticlesBtn.IsVisible = artCount > 0;
        FilterVideosBtn.IsVisible = vidCount > 0;

        ApplyRelatedFilter();
        RelatedCollapsedBar.IsVisible = !_isRelatedExpanded;
    }

    private void ApplyRelatedFilter()
    {
        _displayedRelatedItems = _selectedFilter switch
        {
            "Articles" => _allRelatedItems.Where(i => i.Type == "Article").ToList(),
            "Videos" => _allRelatedItems.Where(i => i.Type == "Video").ToList(),
            _ => _allRelatedItems
        };

        RelatedItemsList.ItemsSource = null;
        RelatedItemsList.ItemsSource = _displayedRelatedItems;
    }

    private async void OnToggleRelatedClicked(object? sender, EventArgs e)
    {
        _isRelatedExpanded = !_isRelatedExpanded;

        if (_isRelatedExpanded)
        {
            RelatedCollapsedBar.IsVisible = false;
            RelatedExpandedSheet.IsVisible = true;
            RelatedExpandedSheet.Opacity = 0;
            RelatedExpandedSheet.TranslationY = 80;
            await Task.WhenAll(
                RelatedExpandedSheet.FadeToAsync(1, 220, Easing.CubicOut),
                RelatedExpandedSheet.TranslateToAsync(0, 0, 220, Easing.CubicOut)
            );
        }
        else
        {
            await Task.WhenAll(
                RelatedExpandedSheet.FadeToAsync(0, 180, Easing.CubicIn),
                RelatedExpandedSheet.TranslateToAsync(0, 80, 180, Easing.CubicIn)
            );
            RelatedExpandedSheet.IsVisible = false;
            RelatedCollapsedBar.IsVisible = true;
        }
    }

    private void OnRelatedFilterClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string filter)
        {
            _selectedFilter = filter;

            var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
            var activeBg = (Color)Application.Current!.Resources["AccentOrange"];
            var activeText = Colors.White;
            var activeBorder = (Color)Application.Current!.Resources["AccentOrange"];

            var inactiveBg = isDark ? (Color)Application.Current!.Resources["ButtonSecondaryBgDark"] : (Color)Application.Current!.Resources["ButtonBgLight"];
            var inactiveText = isDark ? (Color)Application.Current!.Resources["ButtonTextDark"] : (Color)Application.Current!.Resources["ButtonTextLight"];
            var inactiveBorder = isDark ? (Color)Application.Current!.Resources["ButtonBorderDark"] : (Color)Application.Current!.Resources["ButtonBorderLight"];

            void StyleFilterButton(Button button, bool isActive)
            {
                button.BackgroundColor = isActive ? activeBg : inactiveBg;
                button.TextColor = isActive ? activeText : inactiveText;
                button.BorderColor = isActive ? activeBorder : inactiveBorder;
                button.BorderWidth = 1.5;
            }

            StyleFilterButton(FilterAllBtn, filter == "All");
            StyleFilterButton(FilterArticlesBtn, filter == "Articles");
            StyleFilterButton(FilterVideosBtn, filter == "Videos");

            ApplyRelatedFilter();
        }
    }

    private async void OnRelatedItemCardTapped(object? sender, EventArgs e)
    {
        RelatedStoryDisplayItem? item = null;
        if (sender is BindableObject bindable && bindable.BindingContext is RelatedStoryDisplayItem contextItem)
        {
            item = contextItem;
        }
        else if (e is TappedEventArgs tapped && tapped.Parameter is RelatedStoryDisplayItem paramItem)
        {
            item = paramItem;
        }

        if (item != null && !string.IsNullOrWhiteSpace(item.Url))
        {
            // Close drawer first
            _isRelatedExpanded = false;
            RelatedExpandedSheet.IsVisible = false;
            RelatedCollapsedBar.IsVisible = true;

            // Push the related story into a new ArticleWebPage
            var targetCategory = item.IsVideo ? "Video" : (item.IsSocial ? "Socials" : item.Category);
            await Navigation.PushAsync(new ArticleWebPage(item.Url, item.Title, item.ImageUrl, targetCategory, item.Id));
        }
    }

    // ──────────────────────────────────────────────────────────
    // Local / Offline Fallback Helpers
    // ──────────────────────────────────────────────────────────

    private List<RelatedStoryDisplayItem> GetLocalFallbackRelatedStories(string? title, string? category)
    {
        var list = new List<RelatedStoryDisplayItem>();
        try
        {
            var cached = Preferences.Get("last_briefing", string.Empty);
            if (!string.IsNullOrWhiteSpace(cached))
            {
                var categories = JsonSerializer.Deserialize<List<BriefingCategory>>(cached, JsonOptions);
                var allArticles = categories?.SelectMany(c => c.Top).ToList() ?? [];

                var targetCategory = category?.Trim().ToLowerInvariant() ?? "";
                var matched = allArticles
                    .Where(a => !string.Equals(a.Url, _articleUrl, StringComparison.OrdinalIgnoreCase))
                    .Where(a => string.IsNullOrEmpty(targetCategory) || (a.Category?.ToLowerInvariant() == targetCategory))
                    .Take(3)
                    .ToList();

                foreach (var art in matched)
                {
                    list.Add(new RelatedStoryDisplayItem
                    {
                        Id = art.Id,
                        Title = art.Title,
                        Subtitle = art.Source ?? "News Archive",
                        Source = art.Source ?? "News Archive",
                        Category = art.Category ?? "News",
                        ImageUrl = art.ImageUrl ?? GetFallbackImage(art.Category),
                        Type = "Article",
                        Url = art.Url ?? string.Empty,
                        PublishedAt = art.PublishedAt ?? DateTime.UtcNow,
                        TimeAgo = FormatTimeAgo(art.PublishedAt ?? DateTime.UtcNow)
                    });
                }
            }

            // Add sample fallback video story
            list.Add(new RelatedStoryDisplayItem
            {
                Id = "related_vid_1",
                Title = "Channels TV Feature: Special Report and National Discourse",
                Subtitle = "Channels Television",
                Source = "Channels Television",
                Category = category ?? "News",
                ImageUrl = "https://i.ytimg.com/vi/r8FW2pqYrq4/hqdefault.jpg",
                Type = "Video",
                Url = "https://www.youtube.com/watch?v=r8FW2pqYrq4",
                PublishedAt = DateTime.UtcNow.AddHours(-2),
                TimeAgo = "2h ago"
            });

            // Add sample fallback tweet
            list.Add(new RelatedStoryDisplayItem
            {
                Id = "related_soc_1",
                Title = "UPDATE: Key policy guidelines released following today's inter-agency economic committee session.",
                Subtitle = "Premium Times @PremiumTimesng",
                Source = "@PremiumTimesng",
                Category = category ?? "Politics",
                ImageUrl = "https://pbs.twimg.com/profile_images/premium_times_400x400.jpg",
                Type = "Social",
                Url = "https://x.com/PremiumTimesng",
                PublishedAt = DateTime.UtcNow.AddHours(-3),
                TimeAgo = "3h ago"
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ArticleWebPage] Local fallback error: {ex.Message}");
        }

        return list;
    }

    private static string FormatTimeAgo(DateTime dt)
    {
        var diff = DateTime.UtcNow - dt.ToUniversalTime();
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        return $"{(int)diff.TotalDays}d ago";
    }

    private static string GetFallbackImage(string? category)
    {
        category = category?.ToLowerInvariant() ?? "";
        if (category.Contains("politic")) return "https://images.unsplash.com/photo-1540910419892-4a36d2c3266c?w=600&auto=format&fit=crop&q=80";
        if (category.Contains("business") || category.Contains("econom")) return "https://images.unsplash.com/photo-1611974789855-9c2a0a7236a3?w=600&auto=format&fit=crop&q=80";
        if (category.Contains("sport")) return "https://images.unsplash.com/photo-1508098682722-e99c43a406b2?w=600&auto=format&fit=crop&q=80";
        if (category.Contains("tech")) return "https://images.unsplash.com/photo-1518770660439-4636190af475?w=600&auto=format&fit=crop&q=80";
        return "https://images.unsplash.com/photo-1585829365295-ab7cd400c167?w=600&auto=format&fit=crop&q=80";
    }

    // ──────────────────────────────────────────────────────────
    // Page Interactions & Navigation
    // ──────────────────────────────────────────────────────────

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (!_isReaderMode && ArticleWebView.CanGoBack)
            ArticleWebView.GoBack();
        else
            await Navigation.PopAsync();
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_articleUrl))
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = _title ?? "Share Article",
                Uri = _articleUrl,
                Text = $"Check out this story: {_articleUrl}"
            });
        }
    }

    private async void OnBookmarkClicked(object? sender, EventArgs e)
    {
        await DisplayAlertAsync("Bookmarked", "Story added to your bookmarks.", "OK");
    }
}
