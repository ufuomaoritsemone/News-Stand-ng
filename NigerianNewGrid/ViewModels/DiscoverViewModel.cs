using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;
using NigerianNewGrid.Services;

namespace NigerianNewGrid.ViewModels;

/// <summary>
/// ViewModel for the Discover tab (search, category filter, trending videos, keyword alerts).
/// Extracted from DiscoverPage.xaml.cs to satisfy MVVM and SRP (Fix #33).
/// </summary>
public partial class DiscoverViewModel : ObservableObject
{
    private readonly NewsApiClient _apiClient;
    private readonly IKeywordMatchingService _keywordMatchingService;
    private readonly ILogger<DiscoverViewModel> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private List<BriefingItem>      _allItems      = [];
    private List<VideoStoryItem>    _trendingVideos = [];
    private List<VideoStoryItem>    _topNewsVideos  = [];

    // ── Observable Properties ─────────────────────────────────────
    [ObservableProperty] public partial ObservableCollection<BriefingItem> FilteredItems { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<VideoStoryItem> TrendingVideos { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<VideoStoryItem> TopNewsVideos { get; set; } = [];
    [ObservableProperty] public partial string SelectedCategory { get; set; } = "All";
    [ObservableProperty] public partial string SearchQuery { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsLoadingVideos { get; set; }
    [ObservableProperty] public partial bool IsTrendingSectionVisible { get; set; } = true;
    [ObservableProperty] public partial bool IsTopNewsSectionVisible { get; set; } = true;

    public DiscoverViewModel(
        NewsApiClient apiClient,
        IKeywordMatchingService keywordMatchingService,
        ILogger<DiscoverViewModel> logger)
    {
        _apiClient              = apiClient;
        _keywordMatchingService = keywordMatchingService;
        _logger                 = logger;
    }

    // ── Commands ──────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadAsync()
    {
        LoadFromCache();
        await Task.WhenAll(
            LoadTopNewsVideosAsync(),
            LoadTrendingVideosAsync()
        );
    }

    [RelayCommand]
    public void ApplyFilter(string? category = null)
    {
        if (category is not null)
            SelectedCategory = category;

        var filtered = _allItems.AsEnumerable();

        if (!SelectedCategory.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (SelectedCategory.Equals("Opinion", StringComparison.OrdinalIgnoreCase) ||
                SelectedCategory.Equals("Opinions", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(i =>
                    string.Equals(i.ContentType, "Opinion", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(i.Category, "Opinion", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                filtered = filtered.Where(i =>
                    string.Equals(i.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var term = SearchQuery.Trim().ToLowerInvariant();
            filtered = filtered.Where(i =>
                (i.Title?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.Summary?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        FilteredItems = new ObservableCollection<BriefingItem>(filtered.ToList());
    }

    [RelayCommand]
    public void SearchChanged(string query)
    {
        SearchQuery = query;
        ApplyFilter();
    }

    // ── Internal Helpers ──────────────────────────────────────────

    private void LoadFromCache()
    {
        var cached = Preferences.Get(AppPreferenceKeys.LastBriefing, string.Empty);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            try
            {
                var categories = JsonSerializer.Deserialize<List<BriefingCategory>>(cached, JsonOptions);
                _allItems = categories?.SelectMany(c => c.Top).ToList() ?? [];
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached briefing.");
                _allItems = [];
            }
        }

        var cachedTopNews = Preferences.Get(AppPreferenceKeys.LastTopNewsVideos, string.Empty);
        if (!string.IsNullOrWhiteSpace(cachedTopNews))
        {
            try
            {
                _topNewsVideos = JsonSerializer.Deserialize<List<VideoStoryItem>>(cachedTopNews, JsonOptions) ?? [];
                TopNewsVideos  = new ObservableCollection<VideoStoryItem>(_topNewsVideos);
                IsTopNewsSectionVisible = _topNewsVideos.Count > 0;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize cached top news videos.");
            }
        }

        var cachedVideos = Preferences.Get(AppPreferenceKeys.LastTrendingVideos, string.Empty);
        if (!string.IsNullOrWhiteSpace(cachedVideos))
        {
            try
            {
                _trendingVideos = JsonSerializer.Deserialize<List<VideoStoryItem>>(cachedVideos, JsonOptions) ?? [];
                TrendingVideos  = new ObservableCollection<VideoStoryItem>(_trendingVideos);
                IsTrendingSectionVisible = _trendingVideos.Count > 0;
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
            IsTrendingSectionVisible = true;
        }

        if (_topNewsVideos.Count == 0)
        {
            _topNewsVideos = MainViewModel.GetFallbackVideoStories();
            TopNewsVideos = new ObservableCollection<VideoStoryItem>(_topNewsVideos);
            IsTopNewsSectionVisible = true;
        }

        ApplyFilter();
    }

    private async Task LoadTopNewsVideosAsync()
    {
        try
        {
            var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, "http://localhost:56193");
            _apiClient.BaseUrl = baseUrl;

            var videos = await _apiClient.GetVideoStoriesAsync(15);
            if (videos.Count > 0)
            {
                _topNewsVideos = videos;
                TopNewsVideos  = new ObservableCollection<VideoStoryItem>(_topNewsVideos);
                IsTopNewsSectionVisible = true;
                Preferences.Set(AppPreferenceKeys.LastTopNewsVideos, JsonSerializer.Serialize(videos, JsonOptions));
            }
            else if (_topNewsVideos.Count == 0)
            {
                _topNewsVideos = MainViewModel.GetFallbackVideoStories();
                TopNewsVideos  = new ObservableCollection<VideoStoryItem>(_topNewsVideos);
                IsTopNewsSectionVisible = true;
            }
            else
            {
                IsTopNewsSectionVisible = _topNewsVideos.Count > 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading top news videos.");
            if (_topNewsVideos.Count == 0)
            {
                _topNewsVideos = MainViewModel.GetFallbackVideoStories();
                TopNewsVideos  = new ObservableCollection<VideoStoryItem>(_topNewsVideos);
                IsTopNewsSectionVisible = true;
            }
        }
    }

    private async Task LoadTrendingVideosAsync()
    {
        try
        {
            IsLoadingVideos = true;
            var baseUrl = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, "http://localhost:56193");
            _apiClient.BaseUrl = baseUrl;

            var videos = await _apiClient.GetTrendingVideoStoriesAsync(15);
            if (videos.Count > 0)
            {
                _trendingVideos = videos;
                TrendingVideos  = new ObservableCollection<VideoStoryItem>(_trendingVideos);
                IsTrendingSectionVisible = true;
                Preferences.Set(AppPreferenceKeys.LastTrendingVideos, JsonSerializer.Serialize(videos, JsonOptions));
            }
            else if (_trendingVideos.Count == 0)
            {
                _trendingVideos = MainViewModel.GetFallbackVideoStories();
                TrendingVideos  = new ObservableCollection<VideoStoryItem>(_trendingVideos);
                IsTrendingSectionVisible = true;
            }
            else
            {
                IsTrendingSectionVisible = _trendingVideos.Count > 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading trending videos.");
            if (_trendingVideos.Count == 0)
            {
                _trendingVideos = MainViewModel.GetFallbackVideoStories();
                TrendingVideos  = new ObservableCollection<VideoStoryItem>(_trendingVideos);
                IsTrendingSectionVisible = true;
            }
        }
        finally
        {
            IsLoadingVideos = false;
        }
    }
}
