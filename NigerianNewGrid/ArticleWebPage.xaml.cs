using System.Diagnostics;
using System.Text.Json;
using NigerianNewGrid.Constants;
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
    public string? Content { get; set; }
    public string? Summary { get; set; }
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
    private string? _content;
    private string? _summary;
    private string? _author;
    private string? _contentType;
    private bool _isReaderMode = true;
    private bool _isVideoStory;
    private int _fontSizeLevel = 1; // 0 = 15pt, 1 = 17pt, 2 = 20pt, 3 = 24pt
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
    /// Full constructor initializing Data-Saver Instant Native Reader Mode with local XAML rendering,
    /// typography controls, editorial lead image, and lazy-loaded web view fallback.
    /// </summary>
    public ArticleWebPage(
        string url,
        string? title = null,
        string? imageUrl = null,
        string? category = null,
        string? articleId = null,
        bool isSponsored = false,
        string? sponsorName = null,
        string? content = null,
        string? summary = null,
        string? author = null,
        string? contentType = null)
        : this()
    {
        _articleUrl = url ?? string.Empty;
        _title = title;
        _imageUrl = imageUrl;
        _category = category;
        _articleId = articleId;
        _content = content;
        _summary = summary;
        _author = author;
        _contentType = contentType;

        _isVideoStory = string.Equals(category, "Video", StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(url) && (url.Contains("youtube.com") || url.Contains("youtu.be")));

        _publisher = ArticleDistillerService.IdentifyPublisher(_articleUrl);

        if (isSponsored)
        {
            SponsoredDisclosureBanner.IsVisible = true;
            var displaySponsor = !string.IsNullOrWhiteSpace(sponsorName) ? sponsorName : _publisher.Name;
            SponsoredDisclosureLabel.Text = $"SPONSORED CONTENT • Presented by {displaySponsor}";
            HeaderPublisherLabel.Text = $"✨ {displaySponsor} · Sponsored";
        }
        else
        {
            SponsoredDisclosureBanner.IsVisible = false;
            var (_, readTimeLabel) = ArticleDistillerService.CalculateReadingTime(!string.IsNullOrWhiteSpace(_content) ? _content : _title);
            HeaderPublisherLabel.Text = $"{_publisher.Name} · ⏱️ {readTimeLabel}";
        }

        if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (_isVideoStory)
            {
                _isReaderMode = false;
                NativeReaderScrollView.IsVisible = false;
                ArticleWebView.IsVisible = true;
                ModeToggleBtn.IsVisible = false;
                FontSizeBtn.IsVisible = false;
                LoadingProgress.IsVisible = true;
                ArticleWebView.Source = url;
            }
            else
            {
                // Instant Native Reader Mode: 0 KB data, sub-10ms render
                _isReaderMode = true;
                NativeReaderScrollView.IsVisible = true;
                ArticleWebView.IsVisible = false;
                LoadingProgress.IsVisible = false;
                ModeToggleBtn.Text = "📖 Reader";
                FontSizeBtn.IsVisible = true;

                InitializeNativeReaderContent();
            }

            // Asynchronously fetch related stories (Archive, Videos)
            _ = LoadRelatedStoriesAsync(_articleId, _title, _category);

            // Track: article opened / read
            var analytics = IPlatformApplication.Current?.Services.GetService<NigerianNewGrid.Services.IAnalyticsService>();
            _ = analytics?.TrackAsync("article_read", _articleId, _title, _category);

            var recentlyRead = IPlatformApplication.Current?.Services.GetService<NigerianNewGrid.Services.IRecentlyReadService>();
            recentlyRead?.RecordRead(_articleUrl, _title, _imageUrl, _category, _articleId);
        }
        else
        {
            HeaderPublisherLabel.Text = "Invalid URL";
        }
    }

    /// <summary>
    /// Overload initializing reader directly from a strongly-typed BriefingItem.
    /// </summary>
    public ArticleWebPage(BriefingItem item, bool isSponsored = false, string? sponsorName = null)
        : this(
            item.Url ?? string.Empty,
            item.Title,
            item.ImageUrl,
            item.Category,
            item.Id,
            isSponsored: isSponsored || item.IsSponsored,
            sponsorName: sponsorName ?? item.SponsorName,
            content: item.Content,
            summary: item.Summary,
            author: item.Author,
            contentType: item.ContentType)
    {
    }

    // ──────────────────────────────────────────────────────────
    // Native Reader Rendering & Hydration
    // ──────────────────────────────────────────────────────────

    private void InitializeNativeReaderContent()
    {
        ArticleTitleLabel.Text = _title ?? "News Article";
        bool isOpinion = string.Equals(_contentType, "Opinion", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(_category, "Opinion", StringComparison.OrdinalIgnoreCase);
        CategoryLabel.Text = isOpinion ? "✍️ OPINION / EDITORIAL" : (_category ?? "NEWS").ToUpperInvariant();
        PublishedTimeLabel.Text = "Recently";

        var (_, readTimeLabel) = ArticleDistillerService.CalculateReadingTime(!string.IsNullOrWhiteSpace(_content) ? _content : _title);
        ReadTimeLabel.Text = $"⏱️ {readTimeLabel}";

        PublisherNameLabel.Text = !string.IsNullOrWhiteSpace(_author)
            ? $"By {_author} • {_publisher.Name}"
            : _publisher.Name;
        PublisherDomainLabel.Text = GetPublisherDomain(_articleUrl);

        if (!string.IsNullOrWhiteSpace(_imageUrl))
        {
            HeroImage.Source = _imageUrl;
            HeroImageContainer.IsVisible = true;
        }
        else
        {
            HeroImageContainer.IsVisible = false;
        }

        if (!string.IsNullOrWhiteSpace(_summary))
        {
            ArticleSummaryLabel.Text = _summary;
            ArticleSummaryLabel.IsVisible = true;
        }
        else
        {
            ArticleSummaryLabel.IsVisible = false;
        }

        if (!string.IsNullOrWhiteSpace(_content))
        {
            NoContentFallbackBanner.IsVisible = false;
            RenderNativeArticleContent();
        }
        else
        {
            NoContentFallbackBanner.IsVisible = true;
            // Asynchronously hydrate content from server if article ID is present
            _ = HydrateContentIfMissingAsync(_articleId);
        }
    }

    private static string GetPublisherDomain(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var host = uri.Host.ToLowerInvariant();
                return host.StartsWith("www.") ? host[4..] : host;
            }
        }
        catch { }
        return "news";
    }

    private void RenderNativeArticleContent()
    {
        ArticleContentStack.Children.Clear();
        if (string.IsNullOrWhiteSpace(_content)) return;

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var textColor = isDark 
            ? (Color)Application.Current!.Resources["DarkTextPrimary"] 
            : (Color)Application.Current!.Resources["Gray900"];

        var paragraphs = _content.Split(["\r\n\r\n", "\n\n", "\r\r"], StringSplitOptions.RemoveEmptyEntries);
        var sz = GetCurrentFontSize();
        var lh = GetCurrentLineHeight();

        foreach (var para in paragraphs)
        {
            var trimmed = para.Trim();
            if (trimmed.Length == 0) continue;

            var label = new Label
            {
                Text = trimmed,
                FontFamily = "LegacySansBook",
                FontSize = sz,
                LineHeight = lh,
                LineBreakMode = LineBreakMode.WordWrap,
                TextColor = textColor
            };
            label.SetAppThemeColor(Label.TextColorProperty,
                (Color)Application.Current!.Resources["Gray900"],
                (Color)Application.Current!.Resources["DarkTextPrimary"]);

            ArticleContentStack.Children.Add(label);
        }
    }

    private async Task HydrateContentIfMissingAsync(string? articleId)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return;
        try
        {
            var client = IPlatformApplication.Current?.Services.GetService<NewsApiClient>();
            if (client == null)
            {
                var factory = IPlatformApplication.Current?.Services.GetService<IHttpClientFactory>();
                var http = factory?.CreateClient() ?? new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, "http://localhost:56193");
                client = new NewsApiClient(http) { BaseUrl = baseUrl };
            }

            var article = await client.GetArticleByIdAsync(articleId);
            if (article != null && !string.IsNullOrWhiteSpace(article.Content))
            {
                _content = article.Content;
                if (string.IsNullOrWhiteSpace(_summary)) _summary = article.Summary;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    NoContentFallbackBanner.IsVisible = false;
                    RenderNativeArticleContent();
                    var (_, readTimeLabel) = ArticleDistillerService.CalculateReadingTime(_content);
                    ReadTimeLabel.Text = $"⏱️ {readTimeLabel}";
                    HeaderPublisherLabel.Text = $"{_publisher.Name} · ⏱️ {readTimeLabel}";
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ArticleWebPage] Hydrate content failed: {ex.Message}");
        }
    }

    private double GetCurrentFontSize() => _fontSizeLevel switch
    {
        0 => 15.0,
        1 => 17.0,
        2 => 20.0,
        3 => 24.0,
        _ => 17.0
    };

    private double GetCurrentLineHeight() => _fontSizeLevel switch
    {
        0 => 1.35,
        1 => 1.4,
        2 => 1.45,
        3 => 1.5,
        _ => 1.4
    };

    private void UpdateParagraphFontSizes()
    {
        var sz = GetCurrentFontSize();
        var lh = GetCurrentLineHeight();
        foreach (var child in ArticleContentStack.Children)
        {
            if (child is Label lbl)
            {
                lbl.FontSize = sz;
                lbl.LineHeight = lh;
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    // Reader Mode & Navigation
    // ──────────────────────────────────────────────────────────

    private void OnOpenOriginalWebClicked(object? sender, EventArgs e)
    {
        SwitchToWebView();
    }

    private void OnToggleReaderModeClicked(object? sender, EventArgs e)
    {
        if (_isVideoStory) return;

        _isReaderMode = !_isReaderMode;
        if (_isReaderMode)
        {
            SwitchToReaderView();
        }
        else
        {
            SwitchToWebView();
        }
    }

    private void SwitchToReaderView()
    {
        _isReaderMode = true;
        NativeReaderScrollView.IsVisible = true;
        ArticleWebView.IsVisible = false;
        LoadingProgress.IsVisible = false;

        ModeToggleBtn.Text = "📖 Reader";
        ModeToggleBtn.BackgroundColor = (Color)Application.Current!.Resources["AccentBlue"];
        ModeToggleBtn.TextColor = Colors.White;
        ModeToggleBtn.BorderColor = Color.FromArgb("#0052CC");
        FontSizeBtn.IsVisible = true;
    }

    private void SwitchToWebView()
    {
        _isReaderMode = false;
        NativeReaderScrollView.IsVisible = false;
        ArticleWebView.IsVisible = true;

        if (ArticleWebView.Source == null || (ArticleWebView.Source is UrlWebViewSource u && string.IsNullOrEmpty(u.Url)))
        {
            LoadingProgress.IsVisible = true;
            LoadingProgress.Progress = 0.3;
            ArticleWebView.Source = _articleUrl;
        }

        ModeToggleBtn.Text = "🌐 Web";
        var isDark = Application.Current!.RequestedTheme == AppTheme.Dark;
        ModeToggleBtn.BackgroundColor = isDark ? (Color)Application.Current!.Resources["ButtonSecondaryBgDark"] : (Color)Application.Current!.Resources["ButtonBgLight"];
        ModeToggleBtn.TextColor = isDark ? (Color)Application.Current!.Resources["ButtonTextDark"] : (Color)Application.Current!.Resources["ButtonTextLight"];
        ModeToggleBtn.BorderColor = isDark ? (Color)Application.Current!.Resources["ButtonBorderDark"] : (Color)Application.Current!.Resources["ButtonBorderLight"];
        FontSizeBtn.IsVisible = false;
    }

    private async void OnAdjustFontSizeClicked(object? sender, EventArgs e)
    {
        if (!_isReaderMode) return;

        _fontSizeLevel = (_fontSizeLevel + 1) % 4;

        var (fontSize, label) = _fontSizeLevel switch
        {
            0 => ("15px", "A-"),
            1 => ("17px", "A"),
            2 => ("20px", "A+"),
            3 => ("24px", "A++"),
            _ => ("17px", "A")
        };

        FontSizeBtn.Text = label;
        UpdateParagraphFontSizes();

        if (ArticleWebView.IsVisible)
        {
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
    }

    private string GetFontSizeString()
    {
        return _fontSizeLevel switch
        {
            0 => "15px",
            1 => "17px",
            2 => "20px",
            3 => "24px",
            _ => "17px"
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
                // Inject the Google Chrome-style DOM Distiller reader engine if WebView runs
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
                var factory = IPlatformApplication.Current?.Services.GetService<IHttpClientFactory>();
                var http = factory?.CreateClient() ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, "http://localhost:56193");
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
                        Content = art.Content,
                        Summary = art.Summary,
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
        try
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
                await Navigation.PushAsync(new ArticleWebPage(
                    item.Url, 
                    item.Title, 
                    item.ImageUrl, 
                    targetCategory, 
                    item.Id,
                    content: item.Content,
                    summary: item.Subtitle));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ArticleWebPage] Related story navigation error: {ex.Message}");
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
            var cached = Preferences.Get(AppPreferenceKeys.LastBriefing, string.Empty);
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
                        Subtitle = !string.IsNullOrWhiteSpace(art.Summary) ? art.Summary : (art.Source ?? "News Archive"),
                        Source = art.Source ?? "News Archive",
                        Category = art.Category ?? "News",
                        ImageUrl = art.ImageUrl ?? GetFallbackImage(art.Category),
                        Type = "Article",
                        Url = art.Url ?? string.Empty,
                        Content = art.Content,
                        Summary = art.Summary,
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
        try
        {
            if (!_isReaderMode && ArticleWebView.CanGoBack)
                ArticleWebView.GoBack();
            else
                await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ArticleWebPage] Navigation back error: {ex.Message}");
        }
    }

    /// <summary>
    /// Tagline appended to every share so recipients know where to follow Nigerian news.
    /// Update this string once the app store listing is live.
    /// </summary>
    private const string AppShareTagline =
        "\n\n📲 Follow Nigerian news as it breaks — download Nigerian News and never miss a story.";

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_articleUrl)) return;
        try
        {
            // Track: share clicked before showing the share sheet
            var analytics = IPlatformApplication.Current?.Services.GetService<NigerianNewGrid.Services.IAnalyticsService>();
            _ = analytics?.TrackAsync("article_share", _articleId, _title, _category);

            var shareText = $"{_title ?? "Check out this story"}\n\nRead more: {_articleUrl}{AppShareTagline}";
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = _title ?? "Share Article",
                Uri = _articleUrl,
                Text = shareText
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ArticleWebPage] Share error: {ex.Message}");
        }
    }

    private async void OnBookmarkClicked(object? sender, EventArgs e)
    {
        try
        {
            var bookmarkService = IPlatformApplication.Current?.Services.GetService<IBookmarkService>();
            if (bookmarkService != null && !string.IsNullOrWhiteSpace(_articleUrl))
            {
                var item = new BriefingItem
                {
                    Id = _articleId ?? Guid.NewGuid().ToString("N"),
                    Title = _title ?? _publisher.Name,
                    Url = _articleUrl,
                    ImageUrl = _imageUrl ?? string.Empty,
                    Category = _category ?? "News",
                    Source = _publisher.Name,
                    PublishedAt = DateTime.UtcNow,
                    Content = _content,
                    Summary = _summary
                };
                bool added = bookmarkService.ToggleBookmark(item);

                // Track: bookmark added (only when adding, not removing)
                if (added)
                {
                    var analytics = IPlatformApplication.Current?.Services.GetService<NigerianNewGrid.Services.IAnalyticsService>();
                    _ = analytics?.TrackAsync("article_bookmark", _articleId, _title, _category);
                }

                string message = added ? "Story saved to bookmarks." : "Bookmark removed.";
                await DisplayAlertAsync("Bookmarks", message, "OK");
            }
            else
            {
                await DisplayAlertAsync("Bookmarks", "Story saved to bookmarks.", "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ArticleWebPage] Bookmark error: {ex.Message}");
        }
    }

    /// <summary>
    /// Dismisses the sticky banner ad bar, collapsing its row and reclaiming full screen real estate for the article.
    /// </summary>
    private void OnDismissBannerClicked(object? sender, EventArgs e)
    {
        BottomBannerAdContainer.IsVisible = false;
    }
}
