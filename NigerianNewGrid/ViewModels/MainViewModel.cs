using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Helpers;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;
using NigerianNewGrid.Models;
using NigerianNewGrid.Services;

namespace NigerianNewGrid.ViewModels;

/// <summary>
/// ViewModel for the main briefing page.
/// Extracted from MainPage.xaml.cs to satisfy MVVM and SRP (Fix #32).
/// All state and commands are here; the code-behind is navigation-only.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly NewsApiClient _apiClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IKeywordMatchingService _keywordMatchingService;
    private readonly INotificationService _notificationService;
    private readonly IBookmarkService _bookmarkService;
    private readonly IBriefingCacheService _cacheService;
    private readonly ITextToSpeechService _ttsService;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<MainViewModel> _logger;

    private List<BriefingCategory> _allCategories = [];
    private CancellationTokenSource? _loadCts;
    private bool _pendingAudioBriefingAutoPlay;

    private const int PageSize = 15;

    // ── Observable Properties ─────────────────────────────────────
    [ObservableProperty] public partial ObservableCollection<BriefingDateGroup> DateGroups { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<VideoStoryItem> VideoStories { get; set; } = [];
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial bool IsRefreshing { get; set; }
    [ObservableProperty] public partial bool IsSkeletonVisible { get; set; }
    [ObservableProperty] public partial bool IsEmptyStateVisible { get; set; }
    [ObservableProperty] public partial bool IsCategoriesVisible { get; set; }
    [ObservableProperty] public partial bool IsPaginationVisible { get; set; }
    [ObservableProperty] public partial bool IsLoadMoreVisible { get; set; }
    [ObservableProperty] public partial string LoadMoreText { get; set; } = string.Empty;
    [ObservableProperty] public partial string StoriesCountText { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsSpeaking { get; set; }
    [ObservableProperty] public partial bool IsPaused { get; set; }
    [ObservableProperty] public partial string AudioButtonText { get; set; } = "🎧";
    [ObservableProperty] public partial string AudioButtonHint { get; set; } = "Play daily audio briefing";
    [ObservableProperty] public partial int DisplayedCount { get; set; } = PageSize;
    [ObservableProperty] public partial int TotalAvailableStories { get; set; }

    public MainViewModel(
        NewsApiClient apiClient,
        IHttpClientFactory httpClientFactory,
        IKeywordMatchingService keywordMatchingService,
        INotificationService notificationService,
        IBookmarkService bookmarkService,
        IBriefingCacheService cacheService,
        ITextToSpeechService ttsService,
        IAnalyticsService analyticsService,
        ILogger<MainViewModel> logger)
    {
        _apiClient               = apiClient;
        _httpClientFactory       = httpClientFactory;
        _keywordMatchingService  = keywordMatchingService;
        _notificationService     = notificationService;
        _bookmarkService         = bookmarkService;
        _cacheService            = cacheService;
        _ttsService              = ttsService;
        _analyticsService        = analyticsService;
        _logger                  = logger;

        _bookmarkService.BookmarksChanged += OnBookmarkServiceChanged;
        AppNotificationBridge.PlayAudioBriefingRequested += OnPlayAudioBriefingRequested;

        if (NotificationPreferences.AudioBriefingsEnabled)
        {
            _notificationService.ScheduleAudioBriefings();
        }
    }

    // ── Commands ──────────────────────────────────────────────────

    private bool _hasLoadedOnce;

    public async Task InitializeAsync()
    {
        if (_hasLoadedOnce) return;
        _hasLoadedOnce = true;
        await LoadBriefingAsync();
    }

    [RelayCommand]
    public async Task LoadBriefingAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        try
        {
            IsLoading    = true;
            IsRefreshing = true;

            var endpoint = await ResolveApiBaseUrlAsync(ct);
            _apiClient.BaseUrl = endpoint;

            var language = Preferences.Get(AppPreferenceKeys.PreferredLanguage, "English");
            StatusText        = $"Loading {language}…";
            IsSkeletonVisible = true;
            IsEmptyStateVisible = false;
            IsCategoriesVisible = false;

            var categories = await _apiClient.GetDailyBriefingAsync(language, ct);
            if (categories.Count == 0)
            {
                categories = await _cacheService.GetCachedBriefingAsync();
                if (categories.Count == 0)
                    categories = _cacheService.GetFallbackSampleBriefing(language);
            }
            else
            {
                await _cacheService.SaveBriefingAsync(categories);
                Preferences.Set(AppPreferenceKeys.ApiBaseUrl, _apiClient.BaseUrl);
            }

            ct.ThrowIfCancellationRequested();
            _allCategories = categories;
            DisplayedCount = PageSize;

            RefreshDisplayedStories();
            EvaluateKeywordAlerts(_allCategories);
            UpdateTimestampStatus();
            await LoadVideoFeedsAsync(ct);

            if (_pendingAudioBriefingAutoPlay)
            {
                _pendingAudioBriefingAutoPlay = false;
                _ = PlayAudioBriefingAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled cleanly — no user-facing error needed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading briefing.");
            StatusText = $"Error loading briefing: {ex.Message}";
        }
        finally
        {
            IsLoading           = false;
            IsRefreshing        = false;
            IsSkeletonVisible   = false;
            IsCategoriesVisible = true;
        }
    }

    private void OnPlayAudioBriefingRequested()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_allCategories.Count > 0)
            {
                await PlayAudioBriefingAsync();
            }
            else
            {
                _pendingAudioBriefingAutoPlay = true;
                await LoadBriefingAsync();
            }
        });
    }

    [RelayCommand]
    public async Task RefreshAsync() => await LoadBriefingAsync();

    [RelayCommand]
    public void LoadMoreStories()
    {
        DisplayedCount += PageSize;
        RefreshDisplayedStories();
    }

    [RelayCommand]
    public async Task PlayAudioBriefingAsync()
    {
        // 1. If actively speaking -> Pause
        if (_ttsService.IsSpeaking && !_ttsService.IsPaused)
        {
            _ttsService.Pause();
            IsSpeaking = false;
            IsPaused = true;
            UpdateAudioButtonState();
            SemanticScreenReader.Announce("Daily briefing audio paused.");
            return;
        }

        // 2. If paused -> Resume
        if (IsPaused || _ttsService.IsPaused)
        {
            var language = Preferences.Get(AppPreferenceKeys.PreferredLanguage, "English");
            IsSpeaking = true;
            IsPaused = false;
            UpdateAudioButtonState();
            SemanticScreenReader.Announce($"Resuming daily audio briefing in {language}…");

            var completed = await _ttsService.ResumeBriefingAsync(language);
            IsSpeaking = _ttsService.IsSpeaking;
            IsPaused = _ttsService.IsPaused;
            UpdateAudioButtonState();

            if (completed && !IsPaused)
            {
                _ = _analyticsService.TrackAsync("audio_listen_complete", null, $"Daily Audio Briefing ({language})", "Daily Briefing");
            }
            return;
        }

        // 3. Idle / Stopped -> Start Play from beginning
        var rawStories = _allCategories
            .SelectMany(c => c.Top)
            .ToList();

        // Eliminate duplicate stories from competing news sources
        var topStories = StoryDeduplicationHelper.DeduplicateStories(
            rawStories,
            s => s.Title,
            s => s.Summary,
            s => s.Source)
            .Take(8)
            .ToList();

        if (topStories.Count == 0)
            return;

        var prefLanguage = Preferences.Get(AppPreferenceKeys.PreferredLanguage, "English");
        IsSpeaking = true;
        IsPaused = false;
        UpdateAudioButtonState();
        SemanticScreenReader.Announce($"Playing daily audio briefing in {prefLanguage}…");

        var firstStory = topStories.FirstOrDefault();
        _ = _analyticsService.TrackAsync("audio_listen_start", firstStory?.Id, $"Daily Audio Briefing ({prefLanguage})", "Daily Briefing");

        var isFinished = await _ttsService.SpeakBriefingAsync(topStories, prefLanguage);
        IsSpeaking = _ttsService.IsSpeaking;
        IsPaused = _ttsService.IsPaused;
        UpdateAudioButtonState();

        if (isFinished && !IsPaused)
        {
            _ = _analyticsService.TrackAsync("audio_listen_complete", firstStory?.Id, $"Daily Audio Briefing ({prefLanguage})", "Daily Briefing");
        }
    }

    private void UpdateAudioButtonState()
    {
        if (IsSpeaking)
        {
            AudioButtonText = "⏸";
            AudioButtonHint = "Pause daily audio briefing";
        }
        else if (IsPaused)
        {
            AudioButtonText = "▶";
            AudioButtonHint = "Resume daily audio briefing";
        }
        else
        {
            AudioButtonText = "🎧";
            AudioButtonHint = "Play daily audio briefing";
        }
    }

    [RelayCommand]
    public void ToggleBookmark(BriefingItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        bool added = _bookmarkService.ToggleBookmark(item);
        string msg = added ? $"Saved: {item.Title}" : "Bookmark removed.";
        SemanticScreenReader.Announce(msg);
    }

    [RelayCommand]
    public async Task EnsureOnboardingAsync()
    {
        if (Preferences.Get(AppPreferenceKeys.HasSeenOnboarding, false)) return;

        // Onboarding shown from the Page (requires DisplayActionSheet)
        // This method is a hook for code-behind to call after showing the sheet.
        Preferences.Set(AppPreferenceKeys.DailyReminderEnabled, true);
    }

    // ── Internal Helpers ──────────────────────────────────────────

    private void RefreshDisplayedStories()
    {
        var rawStories = _allCategories
            .SelectMany(c => c.Top)
            .ToList();

        // 1. Separate organic and direct-sponsored stories
        var rawOrganic = rawStories
            .Where(s => !s.IsSponsored && !s.IsAdMobPlaceholder)
            .OrderByDescending(i => i.PublishedAt ?? DateTime.MinValue)
            .ToList();

        // Deduplicate organic stories from competing news outlets
        var uniqueOrganic = StoryDeduplicationHelper.DeduplicateStories(
            rawOrganic,
            i => i.Title,
            i => i.Summary,
            i => i.Source)
            .ToList();

        var uniqueSponsored = rawStories
            .Where(s => s.IsSponsored && !s.IsAdMobPlaceholder)
            .GroupBy(s => s.Id)
            .Select(g => g.First())
            .ToList();

        // 2. Arrange feed with deterministic placement (Special slots 1-3, in-feed 5-30, and AdMob placeholders)
        var allUniqueStories = FeedSlotPlacementHelper.ArrangeFeed(
            uniqueOrganic,
            uniqueSponsored,
            includeAdMobPlaceholders: true);

        TotalAvailableStories = allUniqueStories.Count;
        var paginated = allUniqueStories.Take(DisplayedCount).ToList();

        var groups = BuildDateGroups(paginated);
        DateGroups         = new ObservableCollection<BriefingDateGroup>(groups);
        IsEmptyStateVisible = groups.Count == 0;

        // Track impressions for sponsored articles visible in the active feed
        foreach (var story in paginated.Where(s => s.IsSponsored && !s.IsAdMobPlaceholder && !string.IsNullOrWhiteSpace(s.Id)))
        {
            _ = _apiClient.TrackArticleImpressionAsync(story.Id);
        }

        UpdatePaginationState();
    }

    private void UpdatePaginationState()
    {
        if (TotalAvailableStories == 0)
        {
            IsPaginationVisible = false;
            return;
        }

        IsPaginationVisible = true;
        var showing = Math.Min(DisplayedCount, TotalAvailableStories);

        if (showing >= TotalAvailableStories)
        {
            IsLoadMoreVisible   = false;
            StoriesCountText    = $"All {TotalAvailableStories} stories loaded";
        }
        else
        {
            IsLoadMoreVisible   = true;
            LoadMoreText        = $"Load More Stories (Showing {showing} of {TotalAvailableStories})";
            StoriesCountText    = $"Showing {showing} of {TotalAvailableStories} stories";
        }
    }

    private void UpdateTimestampStatus()
    {
        var topTime = _allCategories
            .SelectMany(c => c.Top)
            .Where(i => i.PublishedAt.HasValue && i.PublishedAt.Value > DateTime.MinValue)
            .Select(i => i.PublishedAt!.Value)
            .OrderByDescending(d => d)
            .FirstOrDefault();

        var dt = topTime > DateTime.MinValue ? topTime.ToLocalTime() : DateTime.Now;
        StatusText = $"Updated as at {dt:d MMM yyyy, h:mm tt}";
        SemanticScreenReader.Announce(StatusText);
    }

    private void OnBookmarksChanged()
    {
        // Re-render to reflect updated bookmark states in the UI
        RefreshDisplayedStories();
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
                    match.MatchedKeyword, match.Article.Title,
                    match.Article.Id, match.Article.Url, match.Article.Category);
            }

            if (matches.Count > 0)
                NotificationPreferences.LastKeywordAlertEvaluationUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keyword evaluation error.");
        }
    }

    public static List<BriefingDateGroup> BuildDateGroups(List<BriefingItem> stories) =>
        BriefingDateGroup.BuildDateGroups(stories);

    /// <summary>Returns a human-readable "X ago" string for a given UTC datetime.</summary>
    public static string TimeAgo(DateTime utcTime)
    {
        var diff = DateTime.UtcNow - utcTime;
        if (diff.TotalMinutes < 1)  return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
        if (diff.TotalHours   < 24) return $"{(int)diff.TotalHours} hours ago";
        if (diff.TotalDays    < 7)  return $"{(int)diff.TotalDays} days ago";
        return utcTime.ToString("MMM d");
    }

    // ── API Base URL Resolution (kept in VM so navigation code stays thin) ──
    private async Task<string> ResolveApiBaseUrlAsync(CancellationToken ct = default)
    {
        var saved = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, string.Empty);
        var probe = _httpClientFactory.CreateClient("ProbeClient");

        if (!string.IsNullOrWhiteSpace(saved))
        {
            try
            {
                var resp = await probe.GetAsync($"{saved.TrimEnd('/')}/healthz/liveness", ct);
                if (resp.IsSuccessStatusCode) return saved;
                Preferences.Remove(AppPreferenceKeys.ApiBaseUrl);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug("Health probe failed for saved URL {Url}: {Error}", saved, ex.Message);
                Preferences.Remove(AppPreferenceKeys.ApiBaseUrl);
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

        string? resolved = null;
        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var tasks    = candidates.Select(c => ProbeAsync(probe, c, probeCts.Token)).ToList();
        var remaining = new List<Task<string?>>(tasks);

        while (remaining.Count > 0)
        {
            var completed = await Task.WhenAny(remaining);
            remaining.Remove(completed);
            var result = await completed;
            if (result is not null)
            {
                resolved = result;
                await probeCts.CancelAsync();
                break;
            }
        }

        if (resolved is not null)
        {
            Preferences.Set(AppPreferenceKeys.ApiBaseUrl, resolved);
            return resolved;
        }

        var fallback = DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:56193"
            : "http://localhost:56193";
        Preferences.Set(AppPreferenceKeys.ApiBaseUrl, fallback);
        return fallback;
    }

    private async Task<string?> ProbeAsync(HttpClient client, string candidate, CancellationToken ct)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(1500));
            var response = await client.GetAsync($"{candidate.TrimEnd('/')}/healthz/liveness", timeoutCts.Token);
            return response.IsSuccessStatusCode ? candidate : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug("Probe failed for {Candidate}: {Error}", candidate, ex.Message);
            return null;
        }
    }

    public void TrackArticleClick(string? articleId)
    {
        if (!string.IsNullOrWhiteSpace(articleId))
        {
            _ = _apiClient.TrackArticleClickAsync(articleId);
        }
    }

    private async Task LoadVideoFeedsAsync(CancellationToken ct = default)
    {
        try
        {
            var stories = await _apiClient.GetVideoStoriesAsync(limit: 20, cancellationToken: ct);
            if (stories.Count == 0)
            {
                stories = GetFallbackVideoStories();
            }
            VideoStories = new ObservableCollection<VideoStoryItem>(stories);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Video feeds load failed. Using fallback stories.");
            VideoStories = new ObservableCollection<VideoStoryItem>(GetFallbackVideoStories());
        }
    }

    public static List<VideoStoryItem> GetFallbackVideoStories() =>
    [
        new VideoStoryItem
        {
            Id           = "channels_vid_1",
            VideoId      = "r8FW2pqYrq4",
            Title        = "WCQ Play-Off: 'It’s A Do Or Die Match', South Africa Coach On Nigeria Clash",
            Summary      = "South Africa national team coach speaks ahead of the crucial world cup qualifying match against Super Eagles.",
            VideoUrl     = "https://www.youtube.com/watch?v=r8FW2pqYrq4",
            ThumbnailUrl = "https://i.ytimg.com/vi/r8FW2pqYrq4/hqdefault.jpg",
            ChannelName  = "Channels Television",
            ChannelId    = "channels_tv",
            Duration     = "LIVE / RECENT",
            Category     = "Sports",
            PublishedAt  = DateTime.UtcNow.AddHours(-1)
        },
        new VideoStoryItem
        {
            Id           = "tvc_vid_2",
            VideoId      = "2nrElTSJE58",
            Title        = "TVC News Headline News: National Economy, Markets & Policy Review",
            Summary      = "Comprehensive news coverage and analysis of fiscal reforms and key governance developments across the federation.",
            VideoUrl     = "https://www.youtube.com/watch?v=2nrElTSJE58",
            ThumbnailUrl = "https://i.ytimg.com/vi/2nrElTSJE58/hqdefault.jpg",
            ChannelName  = "TVC News Nigeria",
            ChannelId    = "tvc_news",
            Duration     = "12:10",
            Category     = "News",
            PublishedAt  = DateTime.UtcNow.AddHours(-3)
        },
        new VideoStoryItem
        {
            Id           = "thecable_vid_3",
            VideoId      = "hmjB9EQ66V8",
            Title        = "TheCable Special Feature: Investigative Spotlight and Governance Insights",
            Summary      = "In-depth investigative documentary exploring key policy implementation and socio-economic milestones.",
            VideoUrl     = "https://www.youtube.com/watch?v=hmjB9EQ66V8",
            ThumbnailUrl = "https://i.ytimg.com/vi/hmjB9EQ66V8/hqdefault.jpg",
            ChannelName  = "TheCable",
            ChannelId    = "the_cable",
            Duration     = "09:30",
            Category     = "Politics",
            PublishedAt  = DateTime.UtcNow.AddHours(-5)
        },
        new VideoStoryItem
        {
            Id           = "sahara_vid_4",
            VideoId      = "7LA4EfXNXzc",
            Title        = "SaharaTV Report: National Assembly and Legal Developments",
            Summary      = "Coverage of recent legislative proceedings, public hearings, and civic accountability discussions.",
            VideoUrl     = "https://www.youtube.com/watch?v=7LA4EfXNXzc",
            ThumbnailUrl = "https://i.ytimg.com/vi/7LA4EfXNXzc/hqdefault.jpg",
            ChannelName  = "SaharaTV",
            ChannelId    = "sahara_tv",
            Duration     = "15:20",
            Category     = "Politics",
            PublishedAt  = DateTime.UtcNow.AddHours(-7)
        }
    ];

    private void OnBookmarkServiceChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(OnBookmarksChanged);
    }

    public void Dispose()
    {
        _ttsService.Cancel();
        _bookmarkService.BookmarksChanged -= OnBookmarkServiceChanged;
        AppNotificationBridge.PlayAudioBriefingRequested -= OnPlayAudioBriefingRequested;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }

}
