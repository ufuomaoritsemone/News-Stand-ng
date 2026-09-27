using System.Diagnostics;
using System.Text.Json;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Helpers;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;
using NigerianNewGrid.Services;
using NigerianNewGrid.ViewModels;
using Plugin.AdMob;

namespace NigerianNewGrid;

/// <summary>
/// Discover tab — search + category filter over 7-day / 30-day articles and trending YouTube video stories,
/// including a dedicated 📖 Recently Read tab and 🔔 Keyword Alerts matching user monitored topics.
/// </summary>
public partial class DiscoverPage : ContentPage
{
    private readonly DiscoverViewModel _vm;
    private readonly NewsApiClient _apiClient;
    private readonly IKeywordMatchingService _keywordMatchingService;
    private readonly IRecentlyReadService _recentlyReadService;

    private List<BriefingItem> _allItems = [];
    private List<BriefingItem> _categoryArticles = [];
    private List<BriefingItem> _searchResults = [];
    private List<VideoStoryItem> _trendingVideos = [];
    private List<string> _renderedArticleIds = [];
    private List<BriefingItem> _arrangedCategoryFeed = [];
    private string _selectedCategory = "All";
    private bool _isSearchMode;
    private string _currentSearchQuery = string.Empty;
    private int _searchPage = 1;
    private int _categoryPage = 1;
    private bool _hasMoreCategoryPages = true;
    private bool _isLoadingMore;
    private bool _isInitialized;
    private DateTime _lastFetchTime = DateTime.MinValue;

    private const int CategoryBatchSize = 15;
    private int _displayedCategoryCount = CategoryBatchSize;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static T GetRes<T>(string key, T fallback)
    {
        if (Application.Current?.Resources is { } res &&
            res.TryGetValue(key, out var raw) && raw is T typed)
            return typed;
        return fallback;
    }

    private static ImageSource GetOptimizedImageSource(string? url, string fallback)
    {
        var target = !string.IsNullOrWhiteSpace(url) ? url.Trim() : fallback;
        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            target = "https://" + target.Substring("http://".Length);
        }

        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return new UriImageSource
            {
                Uri = uri,
                CachingEnabled = true,
                CacheValidity = TimeSpan.FromDays(2)
            };
        }
        return ImageSource.FromFile(fallback);
    }

    public DiscoverPage(
        DiscoverViewModel viewModel,
        NewsApiClient apiClient,
        IKeywordMatchingService keywordMatchingService,
        IRecentlyReadService recentlyReadService)
    {
        InitializeComponent();
        _vm                     = viewModel;
        _apiClient              = apiClient;
        _keywordMatchingService = keywordMatchingService;
        _recentlyReadService    = recentlyReadService;
        BindingContext          = _vm;

        Appearing += async (_, _) =>
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                await LoadFromCacheAsync();
            }

            // Refresh recently read or active view
            if (string.Equals(_selectedCategory, "RecentlyRead", StringComparison.OrdinalIgnoreCase))
            {
                LoadRecentlyRead();
            }

            // Stale-check: only re-query network if cache is empty or older than 5 minutes
            if ((DateTime.UtcNow - _lastFetchTime).TotalMinutes >= 5 || _trendingVideos.Count == 0)
            {
                _lastFetchTime = DateTime.UtcNow;
                await LoadTrendingVideosAsync();
            }
        };
    }

    private async Task LoadFromCacheAsync()
    {
        var cached = Preferences.Get(AppPreferenceKeys.LastBriefing, string.Empty);
        var cachedVideos = Preferences.Get(AppPreferenceKeys.LastTrendingVideos, string.Empty);

        var (items, trendingVideos) = await Task.Run(() =>
        {
            List<BriefingItem> items = [];
            List<VideoStoryItem> trending = [];

            if (!string.IsNullOrWhiteSpace(cached))
            {
                try
                {
                    var categories = JsonSerializer.Deserialize<List<BriefingCategory>>(cached, JsonOptions);
                    items = categories?.SelectMany(c => c.Top).ToList() ?? [];
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(cachedVideos))
            {
                try
                {
                    trending = JsonSerializer.Deserialize<List<VideoStoryItem>>(cachedVideos, JsonOptions) ?? [];
                }
                catch { }
            }

            if (trending.Count == 0)
            {
                trending = NigerianNewGrid.ViewModels.MainViewModel.GetFallbackVideoStories();
            }

            return (items, trending);
        });

        _allItems = items;
        _trendingVideos = trendingVideos;

        if (_trendingVideos.Count > 0)
            RenderTrendingVideos(_trendingVideos);

        await LoadCategoryArticlesAsync(_selectedCategory);
    }

    private async Task LoadTrendingVideosAsync()
    {
        try
        {
            var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, "http://localhost:56193");
            _apiClient.BaseUrl = baseUrl;

            var videos = await _apiClient.GetTrendingVideoStoriesAsync(15);
            if (videos.Count > 0)
            {
                bool changed = _trendingVideos.Count != videos.Count ||
                               !_trendingVideos.Select(v => v.Id).SequenceEqual(videos.Select(v => v.Id));

                _trendingVideos = videos;
                Preferences.Set(AppPreferenceKeys.LastTrendingVideos, JsonSerializer.Serialize(videos, JsonOptions));

                if (changed || TrendingVideosLayout.Children.Count == 0)
                {
                    RenderTrendingVideos(_trendingVideos);
                }
            }
            else if (_trendingVideos.Count == 0)
            {
                _trendingVideos = NigerianNewGrid.ViewModels.MainViewModel.GetFallbackVideoStories();
                RenderTrendingVideos(_trendingVideos);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DiscoverPage] Error loading trending videos: {ex.Message}");
            if (_trendingVideos.Count == 0)
            {
                _trendingVideos = NigerianNewGrid.ViewModels.MainViewModel.GetFallbackVideoStories();
                RenderTrendingVideos(_trendingVideos);
            }
        }
    }

    private void RenderTrendingVideos(List<VideoStoryItem> videos)
    {
        TrendingVideosLayout.Children.Clear();

        if (videos.Count == 0)
        {
            TrendingSection.IsVisible = false;
            return;
        }

        TrendingSection.IsVisible = true;

        foreach (var video in videos)
        {
            var card = CreateVideoCard(video);
            TrendingVideosLayout.Children.Add(card);
        }
    }

    private View CreateVideoCard(VideoStoryItem video)
    {
        var cardBorder = new Border
        {
            WidthRequest         = 200,
            MinimumHeightRequest = 240,
            Padding              = 0,
            BackgroundColor      = Colors.White,
            StrokeThickness      = 1,
            Stroke               = GetRes<Brush>("Gray100Brush", new SolidColorBrush(Color.FromArgb("#E4ECE6"))),
            StrokeShape          = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) }
        };

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(126) },
                new RowDefinition { Height = GridLength.Star }
            }
        };

        var imgGrid = new Grid();
        var img = new Image
        {
            Source            = GetOptimizedImageSource(video.DisplayThumbnailUrl, "nigerian_news_icon.png"),
            Aspect            = Aspect.AspectFill,
            HeightRequest     = 126,
            WidthRequest      = 200,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions   = LayoutOptions.Fill
        };
        imgGrid.Children.Add(img);

        var darkOverlay = new Border
        {
            BackgroundColor   = Color.FromRgba(0, 0, 0, 64),
            StrokeThickness   = 0,
            InputTransparent = true,
            VerticalOptions   = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill
        };
        imgGrid.Children.Add(darkOverlay);

        if (video.TrendingRank.HasValue)
        {
            var rankPill = new Border
            {
                Padding           = new Thickness(6, 2),
                BackgroundColor   = Color.FromArgb("#F59E0B"),
                StrokeThickness   = 0,
                StrokeShape       = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(10) },
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions   = LayoutOptions.Start,
                Margin            = new Thickness(8),
                Content           = new Label
                {
                    Text           = $"#{video.TrendingRank}",
                    FontFamily     = "LegacySansBook",
                    FontSize       = 10,
                    FontAttributes = FontAttributes.Bold,
                    TextColor      = Colors.Black
                }
            };
            imgGrid.Children.Add(rankPill);
        }

        var playIcon = new Label
        {
            Text              = "▶",
            FontSize          = 18,
            TextColor         = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions   = LayoutOptions.Center
        };
        imgGrid.Children.Add(playIcon);

        var durPill = new Border
        {
            Padding           = new Thickness(5, 2),
            BackgroundColor   = Color.FromRgba(0, 0, 0, 180),
            StrokeThickness   = 0,
            StrokeShape       = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(6) },
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions   = LayoutOptions.End,
            Margin            = new Thickness(6),
            Content           = new Label
            {
                Text           = string.IsNullOrWhiteSpace(video.Duration) ? "HD" : video.Duration,
                FontFamily     = "LegacySansBook",
                FontSize       = 9,
                FontAttributes = FontAttributes.Bold,
                TextColor      = Colors.White
            }
        };
        imgGrid.Children.Add(durPill);

        grid.Children.Add(imgGrid);
        Grid.SetRow(imgGrid, 0);

        var metaStack = new VerticalStackLayout
        {
            Padding = new Thickness(10, 8),
            Spacing = 3
        };

        var channelLabel = new Label
        {
            Text           = video.ChannelName,
            FontFamily     = "LegacySansBook",
            FontSize       = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor      = GetRes<Color>("AccentBlue", Color.FromArgb("#0066FF")),
            MaxLines       = 1,
            LineBreakMode  = LineBreakMode.TailTruncation
        };

        var titleLabel = new Label
        {
            Text           = video.Title,
            FontFamily     = "LegacySerifBold",
            FontSize       = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor      = GetRes<Color>("Gray900", Color.FromArgb("#212529")),
            MaxLines       = 3,
            LineBreakMode  = LineBreakMode.WordWrap
        };

        string viewsText;
        if (video.ViewCount > 0)
        {
            var formatted = video.ViewCount >= 1000 ? $"{video.ViewCount / 1000.0:F1}K" : video.ViewCount.ToString();
            viewsText = video.IsTrending ? $"🔥 {formatted} views" : $"📺 {formatted} views";
        }
        else
        {
            viewsText = video.IsTrending ? "🔥 Trending" : "📺 Latest Broadcast";
        }

        var viewsLabel = new Label
        {
            Text       = viewsText,
            FontFamily = "LegacySansBook",
            FontSize   = 10,
            TextColor  = GetRes<Color>("Gray500", Color.FromArgb("#6C757D"))
        };

        metaStack.Children.Add(channelLabel);
        metaStack.Children.Add(titleLabel);
        metaStack.Children.Add(viewsLabel);

        grid.Children.Add(metaStack);
        Grid.SetRow(metaStack, 1);

        cardBorder.Content = grid;

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(video.VideoUrl))
            {
                await Navigation.PushAsync(new ArticleWebPage(video.VideoUrl, video.Title, video.ThumbnailUrl, video.Category, video.Id));
            }
        };
        cardBorder.GestureRecognizers.Add(tap);

        return cardBorder;
    }

    private async Task LoadCategoryArticlesAsync(string category)
    {
        _isSearchMode = false;
        _categoryPage = 1;
        _hasMoreCategoryPages = true;
        _categoryArticles.Clear();
        _arrangedCategoryFeed.Clear();
        _renderedArticleIds.Clear();
        LoadMoreBtn.IsVisible = false;

        if (string.Equals(category, "RecentlyRead", StringComparison.OrdinalIgnoreCase))
        {
            LoadRecentlyRead();
            return;
        }

        if (string.Equals(category, "KeywordAlerts", StringComparison.OrdinalIgnoreCase))
        {
            LoadKeywordAlerts();
            return;
        }

        if (string.Equals(category, "Opinion", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "Opinions", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, "http://localhost:56193");
                _apiClient.BaseUrl = baseUrl;

                var articles = await _apiClient.GetOpinionsAsync(days: 14, page: 1, pageSize: 30);
                if (articles.Count > 0)
                {
                    _categoryArticles = articles;
                    _hasMoreCategoryPages = articles.Count >= 30;
                }
                else
                {
                    _categoryArticles = _allItems.Where(i =>
                        string.Equals(i.ContentType, "Opinion", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(i.Category, "Opinion", StringComparison.OrdinalIgnoreCase)).ToList();
                    _hasMoreCategoryPages = false;
                }
            }
            catch
            {
                _categoryArticles = _allItems.Where(i =>
                    string.Equals(i.ContentType, "Opinion", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(i.Category, "Opinion", StringComparison.OrdinalIgnoreCase)).ToList();
                _hasMoreCategoryPages = false;
            }

            _arrangedCategoryFeed = FeedSlotPlacementHelper.ArrangeFeed(_categoryArticles, [], includeAdMobPlaceholders: true);
            RenderArticles(_arrangedCategoryFeed, $"✍️ Editorials & Opinions — Major Outlets ({_categoryArticles.Count})");
            return;
        }

        try
        {
            var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, "http://localhost:56193");
            _apiClient.BaseUrl = baseUrl;

            // Fetch a week's worth of news stories (days = 7, page = 1)
            var catParam = string.Equals(category, "All", StringComparison.OrdinalIgnoreCase) ? null : category;
            var articles = await _apiClient.GetArticlesAsync(category: catParam, days: 7, page: 1, pageSize: 30);

            if (articles.Count > 0)
            {
                _categoryArticles = articles;
                _hasMoreCategoryPages = articles.Count >= 30;
            }
            else
            {
                // Fallback to local cache filtered by category
                _categoryArticles = string.Equals(category, "All", StringComparison.OrdinalIgnoreCase)
                    ? _allItems
                    : _allItems.Where(i => string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
                _hasMoreCategoryPages = false;
            }
        }
        catch
        {
            _categoryArticles = string.Equals(category, "All", StringComparison.OrdinalIgnoreCase)
                ? _allItems
                : _allItems.Where(i => string.Equals(i.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
            _hasMoreCategoryPages = false;
        }

        var headerTitle = string.Equals(category, "All", StringComparison.OrdinalIgnoreCase)
            ? $"All News Articles — Past 7 Days ({_categoryArticles.Count})"
            : $"{category} Articles — Past 7 Days ({_categoryArticles.Count})";

        _arrangedCategoryFeed = FeedSlotPlacementHelper.ArrangeFeed(_categoryArticles, [], includeAdMobPlaceholders: true);
        RenderArticles(_arrangedCategoryFeed, headerTitle);
    }

    private void LoadRecentlyRead()
    {
        _isSearchMode = false;
        _hasMoreCategoryPages = false;
        LoadMoreBtn.IsVisible = false;

        var readStories = _recentlyReadService.GetRecentlyRead().ToList();
        _arrangedCategoryFeed = readStories;
        RenderArticles(readStories, $"📖 Recently Read Stories ({readStories.Count})");
    }

    private void LoadKeywordAlerts()
    {
        _isSearchMode = false;
        _hasMoreCategoryPages = false;
        LoadMoreBtn.IsVisible = false;

        var keywords = NotificationPreferences.MonitoredKeywords;
        if (keywords.Count > 0)
        {
            var pool = _categoryArticles.Count > 0 ? _categoryArticles : _allItems;
            var matches = _keywordMatchingService.EvaluateFreshArticles(
                pool,
                keywords,
                publishedAfterUtc: DateTime.MinValue);

            var list = matches.Select(m => m.Article).ToList();
            var kwSummary = string.Join(", ", keywords);
            _arrangedCategoryFeed = list;
            RenderArticles(list, $"🔔 Monitored Topic Alerts ({list.Count} stories matching: {kwSummary})");
        }
        else
        {
            _arrangedCategoryFeed = [];
            RenderArticles([], "🔔 Monitored Keyword Topics (No active keywords)");
        }
    }

    private async void OnSearchClicked(object? sender, EventArgs e)
    {
        var query = DiscoverSearchEntry?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            await LoadCategoryArticlesAsync(_selectedCategory);
            return;
        }

        _isSearchMode = true;
        _currentSearchQuery = query;
        _searchPage = 1;
        _searchResults.Clear();
        ClearBtn.IsVisible = true;

        ResultsHeaderLabel.Text = $"Searching \"{query}\"...";
        EmptyStateView.IsVisible = false;

        await FetchSearchBatchAsync();
    }

    private async Task FetchSearchBatchAsync()
    {
        try
        {
            var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, "http://localhost:56193");
            _apiClient.BaseUrl = baseUrl;

            string? catParam = null;
            if (!string.Equals(_selectedCategory, "All", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(_selectedCategory, "RecentlyRead", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(_selectedCategory, "KeywordAlerts", StringComparison.OrdinalIgnoreCase))
            {
                catParam = _selectedCategory;
            }

            // Full archive search in batches of 30
            var newItems = await _apiClient.GetArticlesAsync(
                search: _currentSearchQuery,
                category: catParam,
                days: null,
                page: _searchPage,
                pageSize: 30);

            if (_searchPage == 1)
            {
                _searchResults = newItems;
            }
            else
            {
                _searchResults.AddRange(newItems);
            }

            LoadMoreBtn.IsVisible = newItems.Count >= 30;
            LoadMoreBtn.Text = "Load More Stories...";

            var title = string.IsNullOrEmpty(catParam)
                ? $"🔍 Search Results for \"{_currentSearchQuery}\" ({_searchResults.Count})"
                : $"🔍 Search Results for \"{_currentSearchQuery}\" in {catParam} ({_searchResults.Count})";
            RenderArticles(_searchResults, title);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DiscoverPage] Search error: {ex.Message}");
            RenderArticles(_searchResults, $"Search Results ({_searchResults.Count})");
        }
    }

    private async void OnLoadMoreClicked(object? sender, EventArgs e)
    {
        if (_isLoadingMore) return;
        _isLoadingMore = true;

        try
        {
            if (_isSearchMode)
            {
                _searchPage++;
                await FetchSearchBatchAsync();
                return;
            }

            // 1. Render remaining items already loaded in memory for this category
            if (_displayedCategoryCount < _arrangedCategoryFeed.Count)
            {
                var nextBatch = _arrangedCategoryFeed.Skip(_displayedCategoryCount).Take(CategoryBatchSize).ToList();
                _displayedCategoryCount += nextBatch.Count;
                foreach (var item in nextBatch)
                {
                    var card = CreateArticleCard(item);
                    ResultsContainer.Children.Add(card);
                    if (!string.IsNullOrEmpty(item.Id))
                    {
                        _renderedArticleIds.Add(item.Id);
                    }
                }

                LoadMoreBtn.IsVisible = _displayedCategoryCount < _arrangedCategoryFeed.Count || _hasMoreCategoryPages;
                LoadMoreBtn.Text = "Load More Stories...";
                return;
            }

            // 2. Memory items exhausted -> fetch next page from server
            if (_hasMoreCategoryPages &&
                !string.Equals(_selectedCategory, "RecentlyRead", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(_selectedCategory, "KeywordAlerts", StringComparison.OrdinalIgnoreCase))
            {
                LoadMoreBtn.Text = "Loading more stories...";
                _categoryPage++;

                var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, "http://localhost:56193");
                _apiClient.BaseUrl = baseUrl;

                var catParam = string.Equals(_selectedCategory, "All", StringComparison.OrdinalIgnoreCase) ? null : _selectedCategory;
                List<BriefingItem> newArticles;

                if (string.Equals(_selectedCategory, "Opinion", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(_selectedCategory, "Opinions", StringComparison.OrdinalIgnoreCase))
                {
                    newArticles = await _apiClient.GetOpinionsAsync(days: 14, page: _categoryPage, pageSize: 30);
                }
                else
                {
                    newArticles = await _apiClient.GetArticlesAsync(category: catParam, days: 7, page: _categoryPage, pageSize: 30);
                }

                if (newArticles.Count > 0)
                {
                    if (newArticles.Count < 30)
                    {
                        _hasMoreCategoryPages = false;
                    }

                    var uniqueNew = newArticles.Where(a => !_categoryArticles.Any(existing => string.Equals(existing.Id, a.Id, StringComparison.OrdinalIgnoreCase))).ToList();
                    _categoryArticles.AddRange(uniqueNew);

                    var newArranged = FeedSlotPlacementHelper.ArrangeFeed(uniqueNew, [], includeAdMobPlaceholders: true);
                    _arrangedCategoryFeed.AddRange(newArranged);

                    foreach (var item in newArranged)
                    {
                        var card = CreateArticleCard(item);
                        ResultsContainer.Children.Add(card);
                        if (!string.IsNullOrEmpty(item.Id))
                        {
                            _renderedArticleIds.Add(item.Id);
                        }
                    }

                    _displayedCategoryCount += newArranged.Count;
                    LoadMoreBtn.IsVisible = _hasMoreCategoryPages;
                    LoadMoreBtn.Text = "Load More Stories...";

                    var catTitle = string.Equals(_selectedCategory, "All", StringComparison.OrdinalIgnoreCase)
                        ? "All News Articles"
                        : $"{_selectedCategory} Articles";
                    ResultsHeaderLabel.Text = $"{catTitle} — Past 7 Days ({_categoryArticles.Count})";
                }
                else
                {
                    _hasMoreCategoryPages = false;
                    LoadMoreBtn.IsVisible = false;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DiscoverPage] LoadMore error: {ex.Message}");
            LoadMoreBtn.Text = "Load More Stories...";
        }
        finally
        {
            _isLoadingMore = false;
        }
    }

    private void OnScrollViewScrolled(object? sender, ScrolledEventArgs e)
    {
        if (DiscoverScrollView == null || _isLoadingMore) return;
        if (!LoadMoreBtn.IsVisible) return;

        if (DiscoverScrollView.ContentSize.Height > 0)
        {
            var currentPos = e.ScrollY + DiscoverScrollView.Height;
            var threshold = DiscoverScrollView.ContentSize.Height * 0.85;

            if (currentPos >= threshold)
            {
                OnLoadMoreClicked(this, EventArgs.Empty);
            }
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ClearBtn.IsVisible = !string.IsNullOrEmpty(DiscoverSearchEntry?.Text);
    }

    private async void OnClearSearch(object? sender, EventArgs e)
    {
        DiscoverSearchEntry.Text = string.Empty;
        ClearBtn.IsVisible = false;
        await LoadCategoryArticlesAsync(_selectedCategory);
    }

    private async void OnCategoryPillTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string cat) return;
        if (string.Equals(_selectedCategory, cat, StringComparison.OrdinalIgnoreCase) && !_isSearchMode) return;
        _selectedCategory = cat;

        var pills  = new[] { PillAll, PillRecentlyRead, PillKeywordAlerts, PillOpinions, PillPolitics, PillSports, PillBusiness, PillEntertainment, PillTech };
        var labels = new[] { "All", "RecentlyRead", "KeywordAlerts", "Opinion", "Politics", "Sports", "Business", "Entertainment", "Technology" };

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

        // If currently in search mode, filter the active search query by the tapped category
        if (_isSearchMode && !string.IsNullOrWhiteSpace(_currentSearchQuery) &&
            !string.Equals(cat, "RecentlyRead", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(cat, "KeywordAlerts", StringComparison.OrdinalIgnoreCase))
        {
            _searchPage = 1;
            _searchResults.Clear();
            ResultsHeaderLabel.Text = $"Searching \"{_currentSearchQuery}\"...";
            await FetchSearchBatchAsync();
            return;
        }

        await LoadCategoryArticlesAsync(cat);
    }

    private void RenderArticles(List<BriefingItem> articles, string headerText)
    {
        if (articles.Count == 0)
        {
            ResultsContainer.Children.Clear();
            _renderedArticleIds.Clear();
            EmptyStateView.IsVisible     = true;
            ResultsHeaderLabel.IsVisible = false;
            LoadMoreBtn.IsVisible        = false;
            return;
        }

        EmptyStateView.IsVisible     = false;
        ResultsHeaderLabel.IsVisible = true;
        ResultsHeaderLabel.Text      = headerText;

        if (_isSearchMode)
        {
            var searchIds = articles.Select(a => a.Id ?? string.Empty).ToList();
            if (_renderedArticleIds.Count == searchIds.Count && _renderedArticleIds.SequenceEqual(searchIds))
            {
                return;
            }

            _renderedArticleIds = searchIds;
            ResultsContainer.Children.Clear();

            foreach (var item in articles)
            {
                var card = CreateArticleCard(item);
                ResultsContainer.Children.Add(card);
            }
            return;
        }

        // Category batching: render initial batch to keep Android view tree light and avoid scroll jitter
        _displayedCategoryCount = Math.Min(CategoryBatchSize, articles.Count);
        var initialBatch = articles.Take(_displayedCategoryCount).ToList();
        var newIds = initialBatch.Select(a => a.Id ?? string.Empty).ToList();

        if (_renderedArticleIds.Count == newIds.Count && _renderedArticleIds.SequenceEqual(newIds))
        {
            return;
        }

        _renderedArticleIds = newIds;
        ResultsContainer.Children.Clear();

        foreach (var item in initialBatch)
        {
            var card = CreateArticleCard(item);
            ResultsContainer.Children.Add(card);
        }

        LoadMoreBtn.IsVisible = _displayedCategoryCount < articles.Count || _hasMoreCategoryPages;
        LoadMoreBtn.Text = "Load More Stories...";
    }

    private View CreateArticleCard(BriefingItem item)
    {
        if (item.IsAdMobPlaceholder)
        {
            return CreateNativeAdCard();
        }

        var border = new Border
        {
            Padding              = 0,
            BackgroundColor      = GetRes<Color>("White", Colors.White),
            StrokeThickness      = 1,
            Stroke               = GetRes<Brush>("Gray100Brush", new SolidColorBrush(Color.FromArgb("#E4ECE6"))),
            StrokeShape          = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) },
            MinimumHeightRequest = 140
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(130) },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 0
        };

        var imgBorder = new Border
        {
            StrokeThickness = 0,
            StrokeShape     = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16, 0, 16, 0) },
            HeightRequest   = 140,
            WidthRequest    = 130,
            VerticalOptions = LayoutOptions.Fill,
            Content         = new Image
            {
                Source          = GetOptimizedImageSource(item.ImageUrl, "https://images.unsplash.com/photo-1585829365295-ab7cd400c167?w=600"),
                Aspect          = Aspect.AspectFill,
                HeightRequest   = 140,
                WidthRequest    = 130,
                VerticalOptions = LayoutOptions.Fill
            }
        };
        grid.Children.Add(imgBorder);
        Grid.SetColumn(imgBorder, 0);

        var textStack = new VerticalStackLayout
        {
            Padding         = new Thickness(14, 10),
            Spacing         = 4,
            VerticalOptions = LayoutOptions.Center
        };

        bool isOpinion = string.Equals(item.ContentType, "Opinion", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(item.Category, "Opinion", StringComparison.OrdinalIgnoreCase);

        var catLabel = new Label
        {
            Text           = isOpinion ? "✍️ OPINION / EDITORIAL" : (item.Category?.ToUpperInvariant() ?? "NEWS"),
            FontFamily     = "LegacySansBook",
            FontSize       = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor      = isOpinion 
                ? GetRes<Color>("AccentAmber", Color.FromArgb("#D97706")) 
                : GetRes<Color>("AccentBlue", Color.FromArgb("#0066FF"))
        };

        var titleLabel = new Label
        {
            Text           = item.Title,
            FontFamily     = "LegacySerifBold",
            FontSize       = 15,
            FontAttributes = FontAttributes.Bold,
            MaxLines       = 4,
            LineBreakMode  = LineBreakMode.WordWrap,
            TextColor      = GetRes<Color>("Gray900", Color.FromArgb("#212529"))
        };

        var byline = !string.IsNullOrWhiteSpace(item.Author)
            ? $"By {item.Author} • {item.Source}"
            : item.Source;

        var sourceLabel = new Label
        {
            Text       = byline,
            FontFamily = "LegacySansBook",
            FontSize   = 11,
            TextColor  = GetRes<Color>("Gray500", Color.FromArgb("#6C757D"))
        };

        textStack.Children.Add(catLabel);
        textStack.Children.Add(titleLabel);
        textStack.Children.Add(sourceLabel);

        grid.Children.Add(textStack);
        Grid.SetColumn(textStack, 1);

        border.Content = grid;

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(item.Url))
            {
                await Navigation.PushAsync(new ArticleWebPage(item));
            }
        };
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private View CreateNativeAdCard()
    {
        var nativeAdView = new NativeAdView
        {
            AdUnitId = AdConstants.NativeAdUnitId,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var border = new Border
        {
            Padding              = 0,
            BackgroundColor      = GetRes<Color>("White", Colors.White),
            StrokeThickness      = 1,
            Stroke               = GetRes<Brush>("Gray100Brush", new SolidColorBrush(Color.FromArgb("#E4ECE6"))),
            StrokeShape          = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16) }
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(130) },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 0,
            MinimumHeightRequest = 140
        };

        var mediaBorder = new Border
        {
            StrokeThickness = 0,
            StrokeShape     = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(16, 0, 16, 0) },
            HeightRequest   = 140,
            WidthRequest    = 130,
            VerticalOptions = LayoutOptions.Fill,
            Content         = new MediaView
            {
                HeightRequest   = 140,
                WidthRequest    = 130,
                VerticalOptions = LayoutOptions.Fill
            }
        };
        grid.Children.Add(mediaBorder);
        Grid.SetColumn(mediaBorder, 0);

        var textStack = new VerticalStackLayout
        {
            Padding         = new Thickness(14, 10),
            Spacing         = 4,
            VerticalOptions = LayoutOptions.Center
        };

        var badgeBorder = new Border
        {
            Padding = new Thickness(6, 2),
            BackgroundColor = Color.FromArgb("#E6F4EA"),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(6) },
            HorizontalOptions = LayoutOptions.Start,
            Content = new Label
            {
                Text = "AD • SPONSORED",
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#137333")
            }
        };
        textStack.Children.Add(badgeBorder);

        var headlineLabel = new Label
        {
            Text = "Promoted Story",
            FontFamily = "LegacySerifBold",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            MaxLines = 3,
            LineBreakMode = LineBreakMode.WordWrap,
            TextColor = GetRes<Color>("Gray900", Color.FromArgb("#212529"))
        };
        textStack.Children.Add(headlineLabel);

        var ctaLabel = new Label
        {
            Text = "Learn More →",
            FontFamily = "LegacySansBold",
            FontSize = 11,
            TextColor = GetRes<Color>("AccentBlue", Color.FromArgb("#0066FF"))
        };
        textStack.Children.Add(ctaLabel);

        grid.Children.Add(textStack);
        Grid.SetColumn(textStack, 1);

        border.Content = grid;
        if (nativeAdView.AdContent != null)
        {
            nativeAdView.AdContent.Content = border;
        }
        else
        {
            nativeAdView.Content = border;
        }

        return nativeAdView;
    }
}


