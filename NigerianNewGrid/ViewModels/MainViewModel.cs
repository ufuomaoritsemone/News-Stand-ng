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
    private readonly ISonicFeedbackService _sonicService;
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

    // Floating pill notification properties when background worker saves fresh stories
    [ObservableProperty] public partial bool HasNewStoriesAvailable { get; set; }
    [ObservableProperty] public partial int NewStoriesCount { get; set; }
    [ObservableProperty] public partial string NewStoriesPillText { get; set; } = string.Empty;

    public event Action? ScrollToTopRequested;

    public MainViewModel(
        NewsApiClient apiClient,
        IHttpClientFactory httpClientFactory,
        IKeywordMatchingService keywordMatchingService,
        INotificationService notificationService,
        IBookmarkService bookmarkService,
        IBriefingCacheService cacheService,
        ITextToSpeechService ttsService,
        IAnalyticsService analyticsService,
        ISonicFeedbackService sonicService,
        ILogger<MainViewModel> logger)
    {
        Console.WriteLine(">>> [DIAGNOSTIC] MainViewModel constructor START");
        _apiClient               = apiClient;
        _httpClientFactory       = httpClientFactory;
        _keywordMatchingService  = keywordMatchingService;
        _notificationService     = notificationService;
        _bookmarkService         = bookmarkService;
        _cacheService            = cacheService;
        _ttsService              = ttsService;
        _analyticsService        = analyticsService;
        _sonicService            = sonicService;
        _logger                  = logger;

        _bookmarkService.BookmarksChanged += OnBookmarkServiceChanged;
        AppNotificationBridge.PlayAudioBriefingRequested += OnPlayAudioBriefingRequested;
        AppNotificationBridge.NewStoriesAvailable += OnNewStoriesAvailable;
        AppNotificationBridge.AppResumed += OnAppResumedFromBridge;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

        if (NotificationPreferences.AudioBriefingsEnabled)
        {
#if ANDROID
            Android.Util.Log.Info("APP_DEBUG", "MainViewModel ScheduleAudioBriefings");
#endif
            _notificationService.ScheduleAudioBriefings();
        }
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", "MainViewModel constructor FINISHED");
#endif
    }

    // ── Commands ──────────────────────────────────────────────────

    private bool _hasLoadedOnce;

    public async Task InitializeAsync()
    {
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", $"MainViewModel.InitializeAsync START (hasLoadedOnce={_hasLoadedOnce})");
#endif
        if (_hasLoadedOnce) return;
        _hasLoadedOnce = true;
        await LoadBriefingAsync();
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", "MainViewModel.InitializeAsync END");
#endif
    }

    [RelayCommand]
    public async Task LoadBriefingAsync(bool isPullToRefresh = false)
    {
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", "MainViewModel.LoadBriefingAsync START");
#endif
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();

        // Hard 10-second deadline so the spinner is GUARANTEED to stop even if
        // the network hangs or the URL probe waterfall takes too long.
        _loadCts.CancelAfter(TimeSpan.FromSeconds(10));
        var ct = _loadCts.Token;

        try
        {
            var language = Preferences.Get(AppPreferenceKeys.PreferredLanguage, "English");
#if ANDROID
            Android.Util.Log.Info("APP_DEBUG", $"MainViewModel.LoadBriefingAsync language={language}, checking cache");
#endif

            // 1. Instant Cache-First Display (Stale-While-Revalidate)
            if (_allCategories.Count == 0)
            {
#if ANDROID
                Android.Util.Log.Info("APP_DEBUG", "MainViewModel.LoadBriefingAsync calling GetCachedBriefingAsync");
#endif
                var cached = await _cacheService.GetCachedBriefingAsync();
#if ANDROID
                Android.Util.Log.Info("APP_DEBUG", $"MainViewModel.LoadBriefingAsync GetCachedBriefingAsync returned {cached?.Count ?? 0} categories");
#endif
                if (cached.Count > 0)
                {
                    _allCategories = cached;
                    DisplayedCount = PageSize;
                    RefreshDisplayedStories();
                    IsSkeletonVisible = false;
                    IsCategoriesVisible = true;
                    UpdateTimestampStatus();
                }
                else
                {
                    IsSkeletonVisible = true;
                    IsCategoriesVisible = false;
                    StatusText = $"Loading {language}…";
                }
            }
            else
            {
                IsSkeletonVisible = false;
                IsCategoriesVisible = true;
            }

            IsLoading = true;
            if (isPullToRefresh)
            {
                IsRefreshing = true;
            }

            var endpoint = await ResolveApiBaseUrlAsync(ct);
            _apiClient.BaseUrl = endpoint;

            List<BriefingCategory> categories;
            try
            {
                categories = await _apiClient.GetDailyBriefingAsync(language, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Network error fetching daily briefing, maintaining cached stories.");
                categories = [];
            }

            if (categories.Count == 0)
            {
                if (_allCategories.Count == 0)
                {
                    categories = await _cacheService.GetCachedBriefingAsync();
                    if (categories.Count == 0)
                        categories = _cacheService.GetFallbackSampleBriefing(language);
                    _allCategories = categories;
                    await _cacheService.SaveBriefingAsync(_allCategories);
                }
            }
            else
            {
                await _cacheService.SaveBriefingAsync(categories);
                Preferences.Set(AppPreferenceKeys.ApiBaseUrl, _apiClient.BaseUrl);
                _allCategories = categories;
            }

            ct.ThrowIfCancellationRequested();
            DisplayedCount = PageSize;

            RefreshDisplayedStories();
            EvaluateKeywordAlerts(_allCategories);
            UpdateTimestampStatus();
            HasNewStoriesAvailable = false;
            NewStoriesCount = 0;

            if (_pendingAudioBriefingAutoPlay)
            {
                _pendingAudioBriefingAutoPlay = false;
                _ = PlayAudioBriefingAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled cleanly (user cancelled, new refresh started, or 10s deadline hit)
            // If we have cached categories, keep them shown so the user isn't left with blank screen
            if (_allCategories.Count > 0)
            {
                RefreshDisplayedStories();
                UpdateTimestampStatus();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading briefing.");
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            // Reset spinner state BEFORE firing the video load so the UI is
            // responsive immediately. Videos load silently in the background.
            IsLoading           = false;
            IsRefreshing        = false;
            IsSkeletonVisible   = false;
            IsCategoriesVisible = true;

            // Fire video load as a background task — never blocks refresh completion.
            // Uses a fresh token so a subsequent pull-to-refresh can cancel it cleanly.
            _ = LoadVideoFeedsAsync(CancellationToken.None);
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

    private DateTime _lastResumeUtc = DateTime.UtcNow;

    private void OnAppResumedFromBridge()
    {
        OnAppResumed();
    }

    public void OnAppResumed()
    {
        _lastResumeUtc = DateTime.UtcNow;
        _ = CheckForNewStoriesAsync();
    }

    private void OnNewStoriesAvailable(int count)
    {
        _ = CheckForNewStoriesAsync();
    }

    private async Task CheckForNewStoriesAsync()
    {
        // Don't show pill if currently loading or if initial load hasn't completed
        if (_allCategories.Count == 0 || IsLoading || IsRefreshing) return;

        try
        {
            var cached = await _cacheService.GetCachedBriefingAsync(topPerCategory: 10);
            if (cached.Count > 0)
            {
                var displayedIds = new HashSet<string>(
                    _allCategories.SelectMany(c => c.Top)
                                  .Select(s => s.Id)
                                  .Where(id => !string.IsNullOrEmpty(id))
                );

                int newCount = cached.SelectMany(c => c.Top)
                    .Count(s => !string.IsNullOrEmpty(s.Id) && !displayedIds.Contains(s.Id));

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (newCount > 0)
                    {
                        NewStoriesCount = newCount;
                        NewStoriesPillText = newCount == 1 ? "1 new story available" : $"{newCount} new stories available";
                        HasNewStoriesAvailable = true;
                    }
                    else
                    {
                        HasNewStoriesAvailable = false;
                        NewStoriesCount = 0;
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inspect cached briefing for new stories.");
        }
    }

    [RelayCommand]
    public async Task ApplyNewStoriesAsync()
    {
        if (IsLoading || IsRefreshing) return;

        HasNewStoriesAvailable = false;
        NewStoriesCount = 0;

        _ = _sonicService.PlayRefreshChimeAsync();

        // Stale-while-revalidate reload from SQLite cache (capped at top 10 per category)
        var cached = await _cacheService.GetCachedBriefingAsync(topPerCategory: 10);
        if (cached.Count > 0)
        {
            _allCategories = cached;
            DisplayedCount = PageSize;
            RefreshDisplayedStories();
            UpdateTimestampStatus();
        }
        else
        {
            await LoadBriefingAsync(isPullToRefresh: true);
        }

        ScrollToTopRequested?.Invoke();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        // Re-entrancy guard: if already refreshing or loading, bail out
        if (IsRefreshing || IsLoading)
        {
            IsRefreshing = false; // ensure spinner doesn't get stuck from a prior stale state
            return;
        }

        // Ignore spurious refresh commands fired during phone wake-up or resume layout recalculations
        if (DateTime.UtcNow - _lastResumeUtc < TimeSpan.FromSeconds(2.5))
        {
            IsRefreshing = false;
            return;
        }

        _ = _sonicService.PlayRefreshChimeAsync();
        await LoadBriefingAsync(isPullToRefresh: true);
    }

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
        var isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        var allUniqueStories = FeedSlotPlacementHelper.ArrangeFeed(
            uniqueOrganic,
            uniqueSponsored,
            includeAdMobPlaceholders: isOnline);

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
        DateTime receivedTime = DateTime.MinValue;

        var storedUtcStr = Preferences.Get(AppPreferenceKeys.LastBriefingReceivedUtc, string.Empty);
        if (!string.IsNullOrWhiteSpace(storedUtcStr) &&
            DateTime.TryParse(storedUtcStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedUtc))
        {
            receivedTime = parsedUtc.ToLocalTime();
        }
        else
        {
            // Fallback for pre-existing caches without LastBriefingReceivedUtc recorded yet:
            // Extract the latest story publication time that is in the past
            var latestStoryUtc = _allCategories
                .SelectMany(c => c.Top)
                .Where(i => i.PublishedAt.HasValue && i.PublishedAt.Value <= DateTime.UtcNow)
                .Select(i => i.PublishedAt!.Value)
                .OrderByDescending(d => d)
                .FirstOrDefault();

            if (latestStoryUtc > DateTime.MinValue)
            {
                receivedTime = latestStoryUtc.ToLocalTime();
            }
        }

        // Defensive guard: never display a time in the future or an uninitialized min value
        if (receivedTime == DateTime.MinValue || receivedTime > DateTime.Now)
        {
            receivedTime = DateTime.Now;
        }

        StatusText = $"Updated as at {receivedTime:d MMM yyyy, h:mm tt}";
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
#if ANDROID
        Android.Util.Log.Info("APP_DEBUG", "MainViewModel.ResolveApiBaseUrlAsync START");
#endif
        var saved = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, string.Empty);
        var probe = _httpClientFactory.CreateClient("ProbeClient");

        // Fast path: if we already have a working URL, verify it quickly (1.5s timeout)
        // and return immediately on success. Only fall through to the full probe waterfall
        // if the saved URL fails — avoids the 8-10s probe delay on every pull-to-refresh.
        if (!string.IsNullOrWhiteSpace(saved))
        {
            try
            {
                using var quickCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                quickCts.CancelAfter(TimeSpan.FromMilliseconds(1500));
                var resp = await probe.GetAsync($"{saved.TrimEnd('/')}/healthz/liveness", quickCts.Token);
                if (resp.IsSuccessStatusCode) return saved;
            }
            catch (Exception ex) when (ex is not OperationCanceledException { CancellationToken.IsCancellationRequested: true } oce || oce.CancellationToken == ct)
            {
                _logger.LogDebug("Health probe failed for saved URL {Url}: {Error}", saved, ex.Message);
            }
            Preferences.Remove(AppPreferenceKeys.ApiBaseUrl);
        }

        string[] candidates = DeviceInfo.Platform == DevicePlatform.Android
            ? [
                "http://localhost:56193",
                "http://127.0.0.1:56193",
                "http://10.0.2.2:56193",
                "http://localhost:5000",
                "http://127.0.0.1:5000",
                "http://10.0.2.2:5000",
                "http://host.docker.internal:56193",
                "http://host.docker.internal:5000",
                "http://localhost:8080",
                "http://10.0.2.2:8080"
              ]
            : [
                "http://localhost:56193",
                "http://127.0.0.1:56193",
                "http://localhost:5000",
                "http://127.0.0.1:5000",
                "http://host.docker.internal:56193",
                "http://host.docker.internal:5000",
                "http://localhost:8080"
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
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(800));
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

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_allCategories.Count > 0)
            {
                RefreshDisplayedStories();
            }
        });
    }

    public void Dispose()
    {
        _ttsService.Cancel();
        _bookmarkService.BookmarksChanged -= OnBookmarkServiceChanged;
        AppNotificationBridge.PlayAudioBriefingRequested -= OnPlayAudioBriefingRequested;
        AppNotificationBridge.NewStoriesAvailable -= OnNewStoriesAvailable;
        AppNotificationBridge.AppResumed -= OnAppResumedFromBridge;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }

}
