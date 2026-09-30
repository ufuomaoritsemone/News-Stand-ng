using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Helpers;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;
using NigerianNewGrid.Services;

namespace NigerianNewGrid.ViewModels;

/// <summary>
/// ViewModel for the Discover tab (search, category filter, trending videos, keyword alerts, and pagination).
/// Extracted from DiscoverPage.xaml.cs to satisfy clean MVVM separation and SRP (Fix #13 / Fix #33).
/// </summary>
public partial class DiscoverViewModel : ObservableObject
{
    private readonly NewsApiClient _apiClient;
    private readonly IKeywordMatchingService _keywordMatchingService;
    private readonly IRecentlyReadService _recentlyReadService;
    private readonly IBookmarkService _bookmarkService;
    private readonly INewsPersistenceService _persistenceService;
    private readonly ILogger<DiscoverViewModel> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private List<BriefingItem>      _allItems             = [];
    private List<BriefingItem>      _categoryArticles     = [];
    private List<BriefingItem>      _arrangedCategoryFeed = [];
    private List<BriefingItem>      _searchResults        = [];
    private List<VideoStoryItem>    _trendingVideos       = [];

    private int  _searchPage           = 1;
    private int  _categoryPage         = 1;
    private bool _hasMoreCategoryPages = true;
    private const int CategoryBatchSize = 15;
    private int  _displayedCategoryCount = CategoryBatchSize;

    // ── Observable Properties ─────────────────────────────────────
    [ObservableProperty] public partial ObservableCollection<BriefingItem> FilteredItems { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<VideoStoryItem> TrendingVideos { get; set; } = [];
    [ObservableProperty] public partial string SelectedCategory { get; set; } = "All";
    [ObservableProperty] public partial string SearchQuery { get; set; } = string.Empty;
    [ObservableProperty] public partial string ResultsHeader { get; set; } = "Recent News Articles";
    [ObservableProperty] public partial bool IsResultsHeaderVisible { get; set; } = true;
    [ObservableProperty] public partial bool IsEmptyStateVisible { get; set; } = false;
    [ObservableProperty] public partial bool IsLoadMoreVisible { get; set; } = false;
    [ObservableProperty] public partial string LoadMoreText { get; set; } = "Load More Stories...";
    [ObservableProperty] public partial bool IsTrendingSectionVisible { get; set; } = true;
    [ObservableProperty] public partial bool IsClearButtonVisible { get; set; } = false;
    [ObservableProperty] public partial bool IsSearchMode { get; set; } = false;
    [ObservableProperty] public partial bool IsLoading { get; set; } = false;
    [ObservableProperty] public partial bool IsLoadingMore { get; set; } = false;

    public DiscoverViewModel(
        NewsApiClient apiClient,
        IKeywordMatchingService keywordMatchingService,
        IRecentlyReadService recentlyReadService,
        IBookmarkService bookmarkService,
        INewsPersistenceService persistenceService,
        ILogger<DiscoverViewModel> logger)
    {
        _apiClient              = apiClient;
        _keywordMatchingService = keywordMatchingService;
        _recentlyReadService    = recentlyReadService;
        _bookmarkService        = bookmarkService;
        _persistenceService     = persistenceService;
        _logger                 = logger;
    }

    // ── Commands ──────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await LoadFromCacheAsync();
            await LoadTrendingVideosAsync();
            await ApplyCategoryFilterAsync(SelectedCategory);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ApplyCategoryFilterAsync(string? category)
    {
        category ??= "All";
        SelectedCategory = category;
        IsSearchMode = false;
        _categoryPage = 1;
        _hasMoreCategoryPages = true;
        _categoryArticles.Clear();
        _arrangedCategoryFeed.Clear();
        IsLoadMoreVisible = false;
        IsTrendingSectionVisible = category.Equals("All", StringComparison.OrdinalIgnoreCase) && TrendingVideos.Count > 0;

        if (string.Equals(category, "RecentlyRead", StringComparison.OrdinalIgnoreCase))
        {
            var readStories = _recentlyReadService.GetRecentlyRead().ToList();
            _arrangedCategoryFeed = readStories;
            UpdateFeedDisplay(_arrangedCategoryFeed, $"📖 Recently Read Stories ({readStories.Count})");
            return;
        }

        if (string.Equals(category, "KeywordAlerts", StringComparison.OrdinalIgnoreCase))
        {
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
                UpdateFeedDisplay(list, $"🔔 Monitored Topic Alerts ({list.Count} stories matching: {kwSummary})");
            }
            else
            {
                _arrangedCategoryFeed = [];
                UpdateFeedDisplay([], "🔔 Monitored Keyword Topics (No active keywords)");
            }
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

            var isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
            _arrangedCategoryFeed = FeedSlotPlacementHelper.ArrangeFeed(_categoryArticles, [], includeAdMobPlaceholders: isOnline);
            UpdateFeedDisplay(_arrangedCategoryFeed, $"✍️ Editorials & Opinions — Major Outlets ({_categoryArticles.Count})");
            return;
        }

        try
        {
            var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, "http://localhost:56193");
            _apiClient.BaseUrl = baseUrl;

            var catParam = string.Equals(category, "All", StringComparison.OrdinalIgnoreCase) ? null : category;
            var articles = await _apiClient.GetArticlesAsync(category: catParam, days: 7, page: 1, pageSize: 30);

            if (articles.Count > 0)
            {
                _categoryArticles = articles;
                _hasMoreCategoryPages = articles.Count >= 30;
            }
            else
            {
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

        var isConnected = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        _arrangedCategoryFeed = FeedSlotPlacementHelper.ArrangeFeed(_categoryArticles, [], includeAdMobPlaceholders: isConnected);
        UpdateFeedDisplay(_arrangedCategoryFeed, headerTitle);
    }

    [RelayCommand]
    public async Task SearchAsync(string? query)
    {
        query = query?.Trim() ?? string.Empty;
        SearchQuery = query;

        if (string.IsNullOrWhiteSpace(query))
        {
            IsClearButtonVisible = false;
            await ApplyCategoryFilterAsync(SelectedCategory);
            return;
        }

        IsSearchMode = true;
        IsClearButtonVisible = true;
        IsTrendingSectionVisible = false;
        _searchPage = 1;
        _searchResults.Clear();

        ResultsHeader = $"Searching \"{query}\"...";
        IsEmptyStateVisible = false;
        IsResultsHeaderVisible = true;

        await FetchSearchBatchAsync();
    }

    [RelayCommand]
    public async Task ClearSearchAsync()
    {
        SearchQuery = string.Empty;
        IsClearButtonVisible = false;
        IsSearchMode = false;
        await ApplyCategoryFilterAsync(SelectedCategory);
    }

    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (IsLoadingMore) return;
        IsLoadingMore = true;

        try
        {
            if (IsSearchMode)
            {
                _searchPage++;
                await FetchSearchBatchAsync();
                return;
            }

            // In-memory batching: append next batch
            if (_displayedCategoryCount < _arrangedCategoryFeed.Count)
            {
                var nextBatch = _arrangedCategoryFeed.Skip(_displayedCategoryCount).Take(CategoryBatchSize).ToList();
                _displayedCategoryCount += nextBatch.Count;
                foreach (var item in nextBatch)
                {
                    FilteredItems.Add(item);
                }

                IsLoadMoreVisible = _displayedCategoryCount < _arrangedCategoryFeed.Count || _hasMoreCategoryPages;
                LoadMoreText = "Load More Stories...";
                return;
            }

            // Fetch next page from API
            if (_hasMoreCategoryPages &&
                !string.Equals(SelectedCategory, "RecentlyRead", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(SelectedCategory, "KeywordAlerts", StringComparison.OrdinalIgnoreCase))
            {
                LoadMoreText = "Loading more stories...";
                _categoryPage++;

                var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, "http://localhost:56193");
                _apiClient.BaseUrl = baseUrl;

                var catParam = string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase) ? null : SelectedCategory;
                List<BriefingItem> newArticles;

                if (string.Equals(SelectedCategory, "Opinion", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(SelectedCategory, "Opinions", StringComparison.OrdinalIgnoreCase))
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

                    var isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
                    var newArranged = FeedSlotPlacementHelper.ArrangeFeed(uniqueNew, [], includeAdMobPlaceholders: isOnline);
                    _arrangedCategoryFeed.AddRange(newArranged);

                    foreach (var item in newArranged)
                    {
                        FilteredItems.Add(item);
                    }

                    _displayedCategoryCount += newArranged.Count;
                    IsLoadMoreVisible = _hasMoreCategoryPages;
                    LoadMoreText = "Load More Stories...";

                    var catTitle = string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase)
                        ? "All News Articles"
                        : $"{SelectedCategory} Articles";
                    ResultsHeader = $"{catTitle} — Past 7 Days ({_categoryArticles.Count})";
                }
                else
                {
                    _hasMoreCategoryPages = false;
                    IsLoadMoreVisible = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoadMore error.");
            LoadMoreText = "Load More Stories...";
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    public void ToggleBookmark(BriefingItem item)
    {
        _bookmarkService.ToggleBookmark(item);
    }

    // ── Internal Helpers ──────────────────────────────────────────

    private async Task LoadFromCacheAsync()
    {
        try
        {
            var categories = await _persistenceService.GetCachedBriefingAsync(days: 14);
            if (categories.Count > 0)
            {
                _allItems = categories.SelectMany(c => c.Top).ToList();
            }
            else
            {
                var cached = Preferences.Get(AppPreferenceKeys.LastBriefing, string.Empty);
                if (!string.IsNullOrWhiteSpace(cached))
                {
                    var legacyCategories = JsonSerializer.Deserialize<List<BriefingCategory>>(cached, JsonOptions);
                    _allItems = legacyCategories?.SelectMany(c => c.Top).ToList() ?? [];
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load cached briefing from SQLite in DiscoverViewModel.");
            _allItems = [];
        }

        var cachedVideos = Preferences.Get(AppPreferenceKeys.LastTrendingVideos, string.Empty);
        if (!string.IsNullOrWhiteSpace(cachedVideos))
        {
            try
            {
                _trendingVideos = JsonSerializer.Deserialize<List<VideoStoryItem>>(cachedVideos, JsonOptions) ?? [];
                TrendingVideos  = new ObservableCollection<VideoStoryItem>(_trendingVideos);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached trending videos.");
            }
        }

        if (_trendingVideos.Count == 0)
        {
            _trendingVideos = MainViewModel.GetFallbackVideoStories();
            TrendingVideos = new ObservableCollection<VideoStoryItem>(_trendingVideos);
        }

        IsTrendingSectionVisible = _trendingVideos.Count > 0 && SelectedCategory == "All" && !IsSearchMode;
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
                _trendingVideos = videos;
                TrendingVideos  = new ObservableCollection<VideoStoryItem>(_trendingVideos);
                Preferences.Set(AppPreferenceKeys.LastTrendingVideos, JsonSerializer.Serialize(videos, JsonOptions));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading trending videos.");
        }
        finally
        {
            IsTrendingSectionVisible = _trendingVideos.Count > 0 && SelectedCategory == "All" && !IsSearchMode;
        }
    }

    private async Task FetchSearchBatchAsync()
    {
        try
        {
            var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, "http://localhost:56193");
            _apiClient.BaseUrl = baseUrl;

            string? catParam = null;
            if (!string.Equals(SelectedCategory, "All", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(SelectedCategory, "RecentlyRead", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(SelectedCategory, "KeywordAlerts", StringComparison.OrdinalIgnoreCase))
            {
                catParam = SelectedCategory;
            }

            var newItems = await _apiClient.GetArticlesAsync(
                search: SearchQuery,
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

            IsLoadMoreVisible = newItems.Count >= 30;
            LoadMoreText = "Load More Stories...";

            var title = string.IsNullOrEmpty(catParam)
                ? $"🔍 Search Results for \"{SearchQuery}\" ({_searchResults.Count})"
                : $"🔍 Search Results for \"{SearchQuery}\" in {catParam} ({_searchResults.Count})";

            UpdateFeedDisplay(_searchResults, title);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Search error for {Query}", SearchQuery);
            UpdateFeedDisplay(_searchResults, $"Search Results ({_searchResults.Count})");
        }
    }

    private void UpdateFeedDisplay(List<BriefingItem> items, string headerText)
    {
        if (items.Count == 0)
        {
            FilteredItems = [];
            IsEmptyStateVisible     = true;
            IsResultsHeaderVisible = false;
            IsLoadMoreVisible        = false;
            return;
        }

        IsEmptyStateVisible     = false;
        IsResultsHeaderVisible = true;
        ResultsHeader          = headerText;

        if (IsSearchMode)
        {
            FilteredItems = new ObservableCollection<BriefingItem>(items);
            return;
        }

        _displayedCategoryCount = Math.Min(CategoryBatchSize, items.Count);
        var initialBatch = items.Take(_displayedCategoryCount).ToList();
        FilteredItems = new ObservableCollection<BriefingItem>(initialBatch);

        IsLoadMoreVisible = _displayedCategoryCount < items.Count || _hasMoreCategoryPages;
        LoadMoreText = "Load More Stories...";
    }
}
