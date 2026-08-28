using System.Diagnostics;
using System.Text.Json;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Services;

namespace NigerianNewGrid;

public class BriefingDateGroup
{
    public string DateHeader { get; set; } = string.Empty;
    public string RelativeLabel { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public List<BriefingItem> Stories { get; set; } = [];
}

public partial class MainPage : ContentPage
{
    private readonly NewsApiClient _apiClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IKeywordMatchingService _keywordMatchingService;
    private readonly INotificationService _notificationService;
    private readonly IBookmarkService _bookmarkService;
    private readonly IBriefingCacheService _cacheService;
    private readonly ITextToSpeechService _ttsService;

    private List<BriefingCategory> _allCategories = [];
    private CancellationTokenSource? _loadCts;
    private bool _hasLoadedOnce;

    private const int PageSize = 15;
    private int _displayedCount = 15;
    private int _totalAvailableStories = 0;

    public MainPage(
        NewsApiClient apiClient, 
        IHttpClientFactory httpClientFactory,
        IKeywordMatchingService keywordMatchingService,
        INotificationService notificationService,
        IBookmarkService bookmarkService,
        IBriefingCacheService cacheService,
        ITextToSpeechService ttsService)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _httpClientFactory = httpClientFactory;
        _keywordMatchingService = keywordMatchingService;
        _notificationService = notificationService;
        _bookmarkService = bookmarkService;
        _cacheService = cacheService;
        _ttsService = ttsService;

        _bookmarkService.BookmarksChanged += OnBookmarksChanged;

        Appearing += async (_, _) =>
        {
            if (_hasLoadedOnce) return;
            _hasLoadedOnce = true;
            try
            {
                await EnsureOnboardingAsync();
                await LoadBriefingAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainPage load error: {ex}");
                SetStatusError($"Error loading briefing: {ex.Message}");
            }
        };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _bookmarkService.BookmarksChanged -= OnBookmarksChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _bookmarkService.BookmarksChanged += OnBookmarksChanged;
    }

    private void OnBookmarksChanged(object? sender, EventArgs e) => RestoreBookmarks();

    // ──────────────────────────────────────────────────────────
    // Data Loading
    // ──────────────────────────────────────────────────────────

    private async Task LoadBriefingAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        try
        {
            var endpoint = await ResolveApiBaseUrlAsync(ct);
            _apiClient.BaseUrl = endpoint;

            var language = Preferences.Get("preferred_language", "English");
            SetStatusLoading(language);

            SkeletonLoadingView.IsVisible = true;
            EmptyStateView.IsVisible = false;
            CategoriesLayout.IsVisible = false;

            var categories = await _apiClient.GetDailyBriefingAsync(language, ct);
            if (categories.Count == 0)
            {
                categories = await _cacheService.GetCachedBriefingAsync();
                if (categories.Count == 0)
                {
                    categories = _cacheService.GetFallbackSampleBriefing(language);
                }
            }
            else
            {
                await _cacheService.SaveBriefingAsync(categories);
                Preferences.Set("api_base_url", _apiClient.BaseUrl);
            }

            ct.ThrowIfCancellationRequested();
            _allCategories = categories;
            _displayedCount = PageSize; // Reset to first 15 stories on reload

            PopulateStories();
            RestoreBookmarks();
            UpdateTopTimestamp();

            // Evaluate on-device keyword alerts against fresh stories
            EvaluateKeywordAlerts(_allCategories);

            // Load Video feeds dynamically from News API
            await LoadVideoFeedsAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // cancelled cleanly
        }
        finally
        {
            SkeletonLoadingView.IsVisible = false;
            CategoriesLayout.IsVisible = true;
        }
    }

    private void EvaluateKeywordAlerts(List<BriefingCategory> categories)
    {
        if (!NotificationPreferences.KeywordAlertsEnabled) return;

        try
        {
            var allStories = categories
                .SelectMany(c => c.Top)
                .Where(s => !string.IsNullOrWhiteSpace(s.Id))
                .ToList();

            var matches = _keywordMatchingService.EvaluateFreshArticles(
                allStories,
                NotificationPreferences.MonitoredKeywords,
                excludedArticleIds: NotificationPreferences.NotifiedArticleIds);

            foreach (var match in matches)
            {
                NotificationPreferences.RecordNotifiedArticle(match.Article.Id);
                _notificationService.ShowKeywordAlertNotification(
                    match.MatchedKeyword,
                    match.Article.Title,
                    match.Article.Id,
                    match.Article.Url,
                    match.Article.Category);
            }

            if (matches.Count > 0)
            {
                NotificationPreferences.LastKeywordAlertEvaluationUtc = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainPage] Keyword evaluation error: {ex.Message}");
        }
    }

    private static Color GetResourceColor(string key, Color fallback)
    {
        if (Application.Current?.Resources != null &&
            Application.Current.Resources.TryGetValue(key, out var res) &&
            res is Color c)
        {
            return c;
        }
        return fallback;
    }

    private void UpdateTopTimestamp(DateTime? mostRecentPublishedAt = null)
    {
        DateTime dt;
        if (mostRecentPublishedAt.HasValue && mostRecentPublishedAt.Value > DateTime.MinValue)
        {
            dt = mostRecentPublishedAt.Value.ToLocalTime();
        }
        else
        {
            var topTime = _allCategories
                .SelectMany(c => c.Top)
                .Where(i => i.PublishedAt.HasValue && i.PublishedAt.Value > DateTime.MinValue)
                .Select(i => i.PublishedAt!.Value)
                .OrderByDescending(d => d)
                .FirstOrDefault();

            dt = topTime > DateTime.MinValue ? topTime.ToLocalTime() : DateTime.Now;
        }

        string timeFormatted = dt.ToString("d MMM yyyy, h:mm tt");

        var formatted = new FormattedString();
        formatted.Spans.Add(new Span
        {
            Text = "Updated as at ",
            FontSize = 10,
            FontFamily = "LegacySansBook",
            TextColor = GetResourceColor("Gray500", Color.FromArgb("#6C757D"))
        });
        formatted.Spans.Add(new Span
        {
            Text = timeFormatted,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            FontFamily = "LegacySansBook",
            TextColor = GetResourceColor("Gray900", Color.FromArgb("#212529"))
        });

        StatusLabel.FormattedText = formatted;
        SemanticScreenReader.Announce($"Updated as at {timeFormatted}");
    }

    private void SetStatusLoading(string language)
    {
        var formatted = new FormattedString();
        formatted.Spans.Add(new Span
        {
            Text = "Updated as at ",
            FontSize = 10,
            FontFamily = "LegacySansBook",
            TextColor = GetResourceColor("Gray500", Color.FromArgb("#6C757D"))
        });
        formatted.Spans.Add(new Span
        {
            Text = $"Loading {language}…",
            FontSize = 12,
            FontAttributes = FontAttributes.Italic,
            FontFamily = "LegacySansBook",
            TextColor = GetResourceColor("Gray500", Color.FromArgb("#6C757D"))
        });
        StatusLabel.FormattedText = formatted;
    }

    private void SetStatusError(string message)
    {
        var formatted = new FormattedString();
        formatted.Spans.Add(new Span
        {
            Text = "Status: ",
            FontSize = 10,
            FontFamily = "LegacySansBook",
            TextColor = GetResourceColor("Gray500", Color.FromArgb("#6C757D"))
        });
        formatted.Spans.Add(new Span
        {
            Text = message,
            FontSize = 12,
            FontFamily = "LegacySansBook",
            TextColor = GetResourceColor("AccentOrange", Color.FromArgb("#FF7A00"))
        });
        StatusLabel.FormattedText = formatted;
        SemanticScreenReader.Announce(message);
    }

    private void PopulateStories()
    {
        var allUniqueStories = _allCategories
            .SelectMany(c => c.Top)
            .GroupBy(i => !string.IsNullOrWhiteSpace(i.Url) ? i.Url : i.Title)
            .Select(g => g.First())
            .OrderByDescending(i => i.PublishedAt ?? DateTime.MinValue)
            .ToList();

        _totalAvailableStories = allUniqueStories.Count;
        var paginatedStories = allUniqueStories.Take(_displayedCount).ToList();

        var dateGroups = BuildDateGroupsFromStories(paginatedStories);
        BindableLayout.SetItemsSource(CategoriesLayout, dateGroups);
        EmptyStateView.IsVisible = dateGroups.Count == 0;

        UpdatePaginationUi();
    }

    private void UpdatePaginationUi()
    {
        if (_totalAvailableStories == 0)
        {
            PaginationLayout.IsVisible = false;
            return;
        }

        PaginationLayout.IsVisible = true;
        var currentlyShowing = Math.Min(_displayedCount, _totalAvailableStories);

        if (currentlyShowing >= _totalAvailableStories)
        {
            LoadMoreStoriesBtn.IsVisible = false;
            StoriesCountSummaryLabel.Text = $"All {_totalAvailableStories} stories loaded";
        }
        else
        {
            LoadMoreStoriesBtn.IsVisible = true;
            LoadMoreStoriesBtn.Text = $"Load More Stories (Showing {currentlyShowing} of {_totalAvailableStories})";
            StoriesCountSummaryLabel.Text = $"Showing {currentlyShowing} of {_totalAvailableStories} stories";
        }
    }

    private void OnLoadMoreStoriesClicked(object? sender, EventArgs e)
    {
        _displayedCount += PageSize;
        PopulateStories();
    }

    private static List<BriefingDateGroup> BuildDateGroupsFromStories(List<BriefingItem> stories)
    {
        var groups = stories
            .GroupBy(i => (i.PublishedAt?.ToLocalTime().Date) ?? DateTime.Today)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var date = g.Key;
                string dateHeader;
                string relativeLabel;

                if (date == DateTime.Today)
                {
                    relativeLabel = "Today";
                    dateHeader = $"Today · {date:dddd, MMMM d, yyyy}";
                }
                else if (date == DateTime.Today.AddDays(-1))
                {
                    relativeLabel = "Yesterday";
                    dateHeader = $"Yesterday · {date:dddd, MMMM d, yyyy}";
                }
                else
                {
                    relativeLabel = date.ToString("MMM d");
                    dateHeader = date.ToString("dddd, MMMM d, yyyy");
                }

                return new BriefingDateGroup
                {
                    Date = date,
                    DateHeader = dateHeader,
                    RelativeLabel = relativeLabel,
                    Stories = g.OrderByDescending(i => i.PublishedAt ?? DateTime.MinValue).ToList()
                };
            })
            .ToList();

        return groups;
    }

    // ──────────────────────────────────────────────────────────
    // UI Event Handlers
    // ──────────────────────────────────────────────────────────

    private async void OnRefreshClicked(object? sender, EventArgs e) => await LoadBriefingAsync();

    private async void OnPullToRefresh(object? sender, EventArgs e)
    {
        try { await LoadBriefingAsync(); }
        finally { BriefingRefreshView.IsRefreshing = false; }
    }

    private async void OnArticleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not BriefingItem item) return;
        if (string.IsNullOrWhiteSpace(item.Url) || !Uri.TryCreate(item.Url, UriKind.Absolute, out _)) return;
        await Navigation.PushAsync(new ArticleWebPage(item.Url, item.Title, item.ImageUrl, item.Category, item.Id));
    }

    private async void OnPlayDailyAudioBriefingClicked(object? sender, EventArgs e)
    {
        if (_ttsService.IsSpeaking)
        {
            _ttsService.Cancel();
            SemanticScreenReader.Announce("Daily briefing audio paused.");
            return;
        }

        var topStories = _allCategories
            .SelectMany(c => c.Top)
            .Take(8)
            .ToList();

        if (topStories.Count == 0)
        {
            await DisplayAlertAsync("Daily Audio Briefing", "No headlines are available to read right now.", "OK");
            return;
        }

        var language = Preferences.Get("preferred_language", "English");
        SemanticScreenReader.Announce($"Playing daily audio briefing in {language}…");
        await _ttsService.SpeakBriefingAsync(topStories, language);
    }

    private void OnBookmarkClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: BriefingItem item }) return;

        bool added = _bookmarkService.ToggleBookmark(item);
        string message = added ? $"Saved: {item.Title}" : "Bookmark removed.";
        SemanticScreenReader.Announce(message);
    }

    /// <summary>
    /// Tagline appended to every share so recipients know where to follow Nigerian news.
    /// Update this string once the app store listing is live.
    /// </summary>
    private const string AppShareTagline =
        "\n\n📲 Follow Nigerian news as it breaks — download Nigerian News Grid and never miss a story.";

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        BriefingItem? item = null;
        if (sender is Button { CommandParameter: BriefingItem btnItem }) item = btnItem;
        else if (sender is BindableObject { BindingContext: BriefingItem bItem }) item = bItem;
        if (item is null) return;

        var shareText = $"Check out this story from {item.Source ?? "Nigerian News Grid"}:\n\n{item.Title}\n\n{item.Summary}\n\nRead more: {item.Url}{AppShareTagline}".Trim();
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = "Share News Story",
            Text = shareText,
            Uri = !string.IsNullOrWhiteSpace(item.Url) ? item.Url : null
        });
    }

    // ──────────────────────────────────────────────────────────
    // Video Stories Loading & Handlers
    // ──────────────────────────────────────────────────────────

    private async Task LoadVideoFeedsAsync(CancellationToken ct = default)
    {
        try
        {
            var stories = await _apiClient.GetVideoStoriesAsync(limit: 20, cancellationToken: ct);
            if (stories.Count == 0)
            {
                stories = GetFallbackVideoStories();
            }
            VideoFeedsView.ItemsSource = stories;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"Video feeds load failed: {ex.Message}");
            VideoFeedsView.ItemsSource = GetFallbackVideoStories();
        }
    }

    private async void OnVideoStoryTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not VideoStoryItem story) return;
        var targetUrl = !string.IsNullOrWhiteSpace(story.VideoUrl)
            ? story.VideoUrl
            : $"https://www.youtube.com/watch?v={story.VideoId}";

        await Navigation.PushAsync(new ArticleWebPage(targetUrl, story.Title, story.ThumbnailUrl, "Video", story.Id));
    }

    private static List<VideoStoryItem> GetFallbackVideoStories() =>
    [
        new VideoStoryItem
        {
            Id = "channels_vid_1",
            VideoId = "r8FW2pqYrq4",
            Title = "WCQ Play-Off: 'It’s A Do Or Die Match', South Africa Coach On Nigeria Clash",
            Summary = "South Africa national team coach speaks ahead of the crucial world cup qualifying match against Super Eagles.",
            VideoUrl = "https://www.youtube.com/watch?v=r8FW2pqYrq4",
            ThumbnailUrl = "https://i.ytimg.com/vi/r8FW2pqYrq4/hqdefault.jpg",
            ChannelName = "Channels Television",
            ChannelId = "channels_tv",
            Duration = "LIVE / RECENT",
            Category = "Sports",
            PublishedAt = DateTime.UtcNow.AddHours(-1)
        },
        new VideoStoryItem
        {
            Id = "tvc_vid_2",
            VideoId = "2nrElTSJE58",
            Title = "TVC News Headline News: National Economy, Markets & Policy Review",
            Summary = "Comprehensive news coverage and analysis of fiscal reforms and key governance developments across the federation.",
            VideoUrl = "https://www.youtube.com/watch?v=2nrElTSJE58",
            ThumbnailUrl = "https://i.ytimg.com/vi/2nrElTSJE58/hqdefault.jpg",
            ChannelName = "TVC News Nigeria",
            ChannelId = "tvc_news",
            Duration = "12:10",
            Category = "News",
            PublishedAt = DateTime.UtcNow.AddHours(-3)
        },
        new VideoStoryItem
        {
            Id = "thecable_vid_3",
            VideoId = "hmjB9EQ66V8",
            Title = "TheCable Special Feature: Investigative Spotlight and Governance Insights",
            Summary = "In-depth investigative documentary exploring key policy implementation and socio-economic milestones.",
            VideoUrl = "https://www.youtube.com/watch?v=hmjB9EQ66V8",
            ThumbnailUrl = "https://i.ytimg.com/vi/hmjB9EQ66V8/hqdefault.jpg",
            ChannelName = "TheCable",
            ChannelId = "the_cable",
            Duration = "09:30",
            Category = "Politics",
            PublishedAt = DateTime.UtcNow.AddHours(-5)
        },
        new VideoStoryItem
        {
            Id = "sahara_vid_4",
            VideoId = "7LA4EfXNXzc",
            Title = "SaharaTV Report: National Assembly and Legal Developments",
            Summary = "Coverage of recent legislative proceedings, public hearings, and civic accountability discussions.",
            VideoUrl = "https://www.youtube.com/watch?v=7LA4EfXNXzc",
            ThumbnailUrl = "https://i.ytimg.com/vi/7LA4EfXNXzc/hqdefault.jpg",
            ChannelName = "SaharaTV",
            ChannelId = "sahara_tv",
            Duration = "15:20",
            Category = "Politics",
            PublishedAt = DateTime.UtcNow.AddHours(-7)
        }
    ];

    // ──────────────────────────────────────────────────────────
    // Onboarding & Settings
    // ──────────────────────────────────────────────────────────

    private async Task EnsureOnboardingAsync()
    {
        if (Preferences.Get("has_seen_onboarding", false)) return;

        var selection = await DisplayActionSheetAsync(
            "Welcome to Nigerian News Grid",
            "Skip", null,
            "English", "Yoruba", "Igbo", "Hausa");

        var language = !string.IsNullOrWhiteSpace(selection) && selection != "Skip"
            ? selection : "English";

        Preferences.Set("preferred_language", language);
        Preferences.Set("has_seen_onboarding", true);
        Preferences.Set("daily_reminder_enabled", true);
        SemanticScreenReader.Announce($"Welcome! Briefing language: {language}");
    }

    private void RestoreBookmarks()
    {
        // Bookmarks are reactive via IBookmarkService.BookmarksChanged
    }

    // ──────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a human-readable "X ago" string for a given UTC datetime.
    /// </summary>
    public static string TimeAgo(DateTime utcTime)
    {
        var diff = DateTime.UtcNow - utcTime;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hours ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
        return utcTime.ToString("MMM d");
    }

    private async Task<string> ResolveApiBaseUrlAsync(CancellationToken ct = default)
    {
        var savedBaseUrl = Preferences.Get("api_base_url", string.Empty);
        var probeClient = _httpClientFactory.CreateClient("ProbeClient");

        if (!string.IsNullOrWhiteSpace(savedBaseUrl))
        {
            try
            {
                var resp = await probeClient.GetAsync($"{savedBaseUrl.TrimEnd('/')}/healthz/liveness", ct);
                if (resp.IsSuccessStatusCode) return savedBaseUrl;
                Preferences.Remove("api_base_url");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.WriteLine($"Health probe failed: {ex.Message}");
                Preferences.Remove("api_base_url");
            }
        }

        string[] candidates =
        [
            "http://localhost:56193",
            "http://10.0.2.2:56193",
            "http://127.0.0.1:56193",
            "http://host.docker.internal:56193",
            "http://10.0.2.2:5000",
            "http://localhost:5000",
            "http://127.0.0.1:5000",
            "http://host.docker.internal:5000",
            "http://10.0.2.2:8080",
            "http://localhost:8080",
            "http://host.docker.internal:8080"
        ];

        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        string? resolved = null;
        var probeTasks = candidates.Select(async candidate =>
        {
            try
            {
                var response = await probeClient.GetAsync(
                    $"{candidate.TrimEnd('/')}/healthz/liveness", probeCts.Token);
                return response.IsSuccessStatusCode ? candidate : null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Debug.WriteLine($"Probe failed for {candidate}: {ex.Message}");
                return null;
            }
        }).ToList();

        // Pick the first successful probe and cancel the rest
        var remaining = new List<Task<string?>>(probeTasks);
        while (remaining.Count > 0)
        {
            var completed = await Task.WhenAny(remaining);
            remaining.Remove(completed);
            var result = await completed;
            if (result != null)
            {
                resolved = result;
                await probeCts.CancelAsync();
                break;
            }
        }

        if (resolved != null)
        {
            Preferences.Set("api_base_url", resolved);
            return resolved;
        }

        var fallback = DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:56193" : "http://localhost:56193";
        Preferences.Set("api_base_url", fallback);
        return fallback;
    }
}
