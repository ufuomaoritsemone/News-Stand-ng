using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Helpers;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;
using NigerianNewGrid.Extensions;
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
    private readonly IKeywordAlertCoordinator _keywordAlertCoordinator;
    private readonly INotificationService _notificationService;
    private readonly IBookmarkService _bookmarkService;
    private readonly IBriefingCacheService _cacheService;
    private readonly IUserPreferencesService _userPreferences;
    private readonly IAppEventBus _eventBus;
    private readonly IAudioBriefingCoordinator _audioCoordinator;
    private readonly ISonicFeedbackService _sonicService;
    private readonly IArticleInteractionService _interactionService;
    private readonly ILogger<MainViewModel> _logger;

    private List<BriefingCategory> _allCategories = [];
    private List<BriefingItem> _arrangedFeed = [];
    private string? _currentFeedLanguage;
    private string? _resolvedApiBaseUrl;
    private CancellationTokenSource? _loadCts;
    private bool _pendingAudioBriefingAutoPlay;

    private const int PageSize = 15;
    private const int MaxFeedCapacity = FeedQueueHelper.DefaultMaxFeedCapacity; // 60

    // ── Observable Properties ─────────────────────────────────────
    [ObservableProperty] public partial ObservableCollection<BriefingDateGroup> DateGroups { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<VideoStoryItem> VideoStories { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<TrendingStoryItem> TrendingStories { get; set; } = [];
    [ObservableProperty] public partial bool HasTrendingStories { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial bool IsRefreshing { get; set; }
    [ObservableProperty] public partial bool IsSkeletonVisible { get; set; }
    [ObservableProperty] public partial bool IsEmptyStateVisible { get; set; }
    [ObservableProperty] public partial bool IsCategoriesVisible { get; set; }
    [ObservableProperty] public partial bool IsPaginationVisible { get; set; }
    [ObservableProperty] public partial bool IsLoadMoreVisible { get; set; }
    [ObservableProperty] public partial bool IsLoadingMore { get; set; }
    [ObservableProperty] public partial string LoadMoreText { get; set; } = string.Empty;
    [ObservableProperty] public partial string StoriesCountText { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsSpeaking { get; set; }
    [ObservableProperty] public partial bool IsPaused { get; set; }
    [ObservableProperty] public partial string AudioButtonText { get; set; } = "🎧";
    [ObservableProperty] public partial string AudioButtonIcon { get; set; } = "ic_headphones.png";
    [ObservableProperty] public partial string AudioButtonHint { get; set; } = "Play daily audio briefing";
    [ObservableProperty] public partial int DisplayedCount { get; set; } = PageSize;
    [ObservableProperty] public partial int TotalAvailableStories { get; set; }

    // Live Naira FX Rate Pill & Multi-Currency Breakdown Strip (Priority 4.3)
    public const string OfflineNairaFxPillText = "₦0/$";
    public const string OfflineNairaFxParallelSummary = "Parallel: $ ₦0 · £ ₦0 · € ₦0";
    public const string OfflineNairaFxOfficialSummary = "CBN NAFEM: ₦0/$";

    [ObservableProperty] public partial bool HasNairaFxData { get; set; } = true;
    [ObservableProperty] public partial bool IsNairaFxExpanded { get; set; }
    [ObservableProperty] public partial string NairaFxPillText { get; set; } = OfflineNairaFxPillText;
    [ObservableProperty] public partial string NairaFxParallelSummary { get; set; } = OfflineNairaFxParallelSummary;
    [ObservableProperty] public partial string NairaFxOfficialSummary { get; set; } = OfflineNairaFxOfficialSummary;

    // Floating pill notification properties when background worker saves fresh stories
    [ObservableProperty] public partial bool HasNewStoriesAvailable { get; set; }
    [ObservableProperty] public partial int NewStoriesCount { get; set; }
    [ObservableProperty] public partial string NewStoriesPillText { get; set; } = string.Empty;

    // Direct Sponsorship Areas (Hero Spotlight & Partner Perspectives Shelf)
    [ObservableProperty] public partial BriefingItem? HeroSpotlightCampaign { get; set; }
    [ObservableProperty] public partial bool HasHeroSpotlight { get; set; }
    [ObservableProperty] public partial ObservableCollection<BriefingItem> PartnerShelfCampaigns { get; set; } = [];
    [ObservableProperty] public partial bool HasPartnerShelf { get; set; }

    public event Action? ScrollToTopRequested;

    public MainViewModel(
        NewsApiClient apiClient,
        IHttpClientFactory httpClientFactory,
        IKeywordAlertCoordinator keywordAlertCoordinator,
        INotificationService notificationService,
        IBookmarkService bookmarkService,
        IBriefingCacheService cacheService,
        IUserPreferencesService userPreferences,
        IAppEventBus eventBus,
        IAudioBriefingCoordinator audioCoordinator,
        ISonicFeedbackService sonicService,
        IArticleInteractionService interactionService,
        ILogger<MainViewModel> logger)
    {
        _logger                  = logger;
        _logger.LogDebug("MainViewModel constructor START");
        _apiClient               = apiClient;
        _httpClientFactory       = httpClientFactory;
        _keywordAlertCoordinator = keywordAlertCoordinator;
        _notificationService     = notificationService;
        _bookmarkService         = bookmarkService;
        _cacheService            = cacheService;
        _userPreferences         = userPreferences;
        _eventBus                = eventBus;
        _audioCoordinator        = audioCoordinator;
        _sonicService            = sonicService;
        _interactionService      = interactionService;

        _audioCoordinator.StateChanged += OnAudioCoordinatorStateChanged;
        _bookmarkService.BookmarksChanged += OnBookmarkServiceChanged;
        _eventBus.PlayAudioBriefingRequested += OnPlayAudioBriefingRequested;
        _eventBus.NewStoriesAvailable += OnNewStoriesAvailable;
        _eventBus.AppResumed += OnAppResumedFromBridge;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

        if (_userPreferences.AudioBriefingsEnabled)
        {
            _logger.LogDebug("MainViewModel ScheduleAudioBriefings");
            _notificationService.ScheduleAudioBriefings();
        }
        _logger.LogDebug("MainViewModel constructor FINISHED");
    }

    // ── Commands ──────────────────────────────────────────────────

    private bool _hasLoadedOnce;

    public async Task InitializeAsync()
    {
        _logger.LogDebug("MainViewModel.InitializeAsync START (hasLoadedOnce={HasLoadedOnce})", _hasLoadedOnce);
        if (_hasLoadedOnce) return;
        _hasLoadedOnce = true;
        await LoadBriefingAsync();
        _logger.LogDebug("MainViewModel.InitializeAsync END");
    }

    [RelayCommand]
    public async Task LoadBriefingAsync(bool isPullToRefresh = false)
    {
        _logger.LogDebug("MainViewModel.LoadBriefingAsync START");
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
            if (_currentFeedLanguage != language)
            {
                _arrangedFeed = [];
                _currentFeedLanguage = language;
            }
            _logger.LogDebug("MainViewModel.LoadBriefingAsync language={Language}, checking cache", language);

            // 1. Instant Cache-First Display (Stale-While-Revalidate)
            if (_allCategories.Count == 0)
            {
                _logger.LogDebug("MainViewModel.LoadBriefingAsync calling GetCachedBriefingAsync");
                var cached = await _cacheService.GetCachedBriefingAsync(maxTotalStories: MaxFeedCapacity);
                _logger.LogDebug("MainViewModel.LoadBriefingAsync GetCachedBriefingAsync returned {Count} categories", cached?.Count ?? 0);
                if (cached is { Count: > 0 })
                {
                    _allCategories = cached;
                    DisplayedCount = PageSize;
                    EnsureFallbackTrendingStories(_allCategories);
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
                    categories = await _cacheService.GetCachedBriefingAsync(maxTotalStories: MaxFeedCapacity);
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

            EnsureFallbackTrendingStories(_allCategories);
            RefreshDisplayedStories();
            _keywordAlertCoordinator.EvaluateKeywordAlerts(_allCategories);
            UpdateTimestampStatus(isPullToRefresh);
            HasNewStoriesAvailable = false;
            NewStoriesCount = 0;

            if (_pendingAudioBriefingAutoPlay)
            {
                _pendingAudioBriefingAutoPlay = false;
                PlayAudioBriefingAsync().SafeFireAndForget(_logger, "PendingAudioBriefingAutoPlay");
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled cleanly (user cancelled, new refresh started, or 10s deadline hit)
            // If we have cached categories, keep them shown so the user isn't left with blank screen
            if (_allCategories.Count > 0)
            {
                RefreshDisplayedStories();
                UpdateTimestampStatus(isPullToRefresh);
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

            // Fire video, trending story, sponsored campaign & Naira FX loads as background tasks — never blocks refresh completion.
            LoadVideoFeedsAsync(CancellationToken.None).SafeFireAndForget(_logger, "LoadVideoFeeds");
            LoadTrendingStoriesAsync(CancellationToken.None).SafeFireAndForget(_logger, "LoadTrendingStories");
            LoadSponsoredCampaignsAsync(CancellationToken.None).SafeFireAndForget(_logger, "LoadSponsoredCampaigns");
            LoadNairaFxSummaryAsync(CancellationToken.None).SafeFireAndForget(_logger, "LoadNairaFxSummary");
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
        CheckForNewStoriesAsync().SafeFireAndForget(_logger, "CheckForNewStories:Resume");
    }

    private void OnNewStoriesAvailable(int count)
    {
        CheckForNewStoriesAsync(count).SafeFireAndForget(_logger, "CheckForNewStories:NewAvailable");
    }

    private async Task CheckForNewStoriesAsync(int? fallbackCount = null)
    {
        // Don't show pill if currently loading or if initial load hasn't completed
        if (_allCategories.Count == 0 || IsLoading || IsRefreshing) return;

        try
        {
            var cached = await _cacheService.GetCachedBriefingAsync(topPerCategory: 10, maxTotalStories: MaxFeedCapacity);
            if (cached.Count > 0)
            {
                var candidateStories = cached.SelectMany(c => c.Top).ToList();
                int newCount = FeedQueueHelper.CountNewStories(_arrangedFeed, candidateStories, onlyNewerThanFeed: true);
                if (newCount == 0 && fallbackCount.HasValue && fallbackCount.Value > 0)
                {
                    newCount = fallbackCount.Value;
                }

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
            else if (fallbackCount.HasValue && fallbackCount.Value > 0)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    NewStoriesCount = fallbackCount.Value;
                    NewStoriesPillText = fallbackCount.Value == 1 ? "1 new story available" : $"{fallbackCount.Value} new stories available";
                    HasNewStoriesAvailable = true;
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

        _sonicService.PlayRefreshChimeAsync().SafeFireAndForget(_logger, "ApplyNewStories:Chime");

        // Stale-while-revalidate reload from SQLite cache (capped at top 10 per category, max 60 total)
        var cached = await _cacheService.GetCachedBriefingAsync(topPerCategory: 10, maxTotalStories: MaxFeedCapacity);
        if (cached.Count > 0)
        {
            _allCategories = cached;
            DisplayedCount = PageSize;
            RefreshDisplayedStories();
            UpdateTimestampStatus(isUserInitiated: true);
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

        _sonicService.PlayRefreshChimeAsync().SafeFireAndForget(_logger, "Refresh:Chime");
        await LoadBriefingAsync(isPullToRefresh: true);
    }

    [RelayCommand]
    public void LoadMoreStories()
    {
        if (DisplayedCount >= TotalAvailableStories || TotalAvailableStories == 0) return;

        var previousCount = DisplayedCount;
        DisplayedCount = Math.Min(DisplayedCount + PageSize, MaxFeedCapacity);

        // Append the next batch into the existing DateGroups WITHOUT clearing them,
        // so that the scroll position remains completely stable and doesn't jump back to the top!
        AppendDisplayedStories(previousCount, DisplayedCount);
    }

    [RelayCommand]
    public async Task PlayAudioBriefingAsync()
    {
        var prefLanguage = Preferences.Get(AppPreferenceKeys.PreferredLanguage, "English");
        await _audioCoordinator.TogglePlayPauseAsync(_allCategories, prefLanguage);
    }

    private void OnAudioCoordinatorStateChanged(object? sender, EventArgs e)
    {
        IsSpeaking = _audioCoordinator.IsSpeaking;
        IsPaused = _audioCoordinator.IsPaused;
        AudioButtonText = _audioCoordinator.AudioButtonText;
        AudioButtonIcon = _audioCoordinator.AudioButtonIcon;
        AudioButtonHint = _audioCoordinator.AudioButtonHint;
    }

    [RelayCommand]
    public async Task OpenArticleAsync(BriefingItem? item)
    {
        await _interactionService.OpenArticleAsync(item);
    }

    [RelayCommand]
    public async Task OpenVideoAsync(VideoStoryItem? story)
    {
        await _interactionService.OpenVideoAsync(story);
    }

    [RelayCommand]
    public void ToggleBookmark(BriefingItem? item)
    {
        _interactionService.ToggleBookmark(item);
    }

    [RelayCommand]
    public async Task ShareArticleAsync(BriefingItem? item)
    {
        await _interactionService.ShareArticleAsync(item);
    }

    [RelayCommand]
    public async Task ShareArticleOnXAsync(BriefingItem? item)
    {
        await _interactionService.ShareArticleOnXAsync(item);
    }

    [RelayCommand]
    public async Task ShareArticleOnWhatsAppAsync(BriefingItem? item)
    {
        await _interactionService.ShareArticleOnWhatsAppAsync(item);
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
        // Run heavy LINQ/sorting/grouping on a background thread to keep the UI responsive.
        // The results are marshalled back to the UI thread for collection update.
        var allCategories = _allCategories;
        var currentFeed = _arrangedFeed;
        var displayedCount = DisplayedCount;
        var isOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        Task.Run(() =>
        {
            var rawStories = allCategories
                .SelectMany(c => c.Top)
                .ToList();

            var arrangedFeed = FeedQueueHelper.EnqueueWithEviction(
                currentFeed: currentFeed,
                incomingStories: rawStories,
                includeAdMobPlaceholders: isOnline,
                maxCapacity: MaxFeedCapacity);

            var totalAvailable = Math.Min(arrangedFeed.Count, MaxFeedCapacity);
            var paginated = arrangedFeed.Take(displayedCount).ToList();
            var groups = BuildDateGroups(paginated);

            // Collect sponsored article IDs for impression tracking
            var sponsoredIds = paginated
                .Where(s => s.IsSponsored && !s.IsAdMobPlaceholder && !string.IsNullOrWhiteSpace(s.Id))
                .Select(s => s.Id)
                .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _arrangedFeed = arrangedFeed;
                TotalAvailableStories = totalAvailable;

                bool isIdentical = DateGroups.Count == groups.Count && DateGroups.Count > 0;
                if (isIdentical)
                {
                    for (int i = 0; i < groups.Count; i++)
                    {
                        if (DateGroups[i].Date != groups[i].Date || DateGroups[i].Count != groups[i].Count)
                        {
                            isIdentical = false;
                            break;
                        }
                        for (int j = 0; j < groups[i].Count; j++)
                        {
                            if (!string.Equals(DateGroups[i][j].Id, groups[i][j].Id, StringComparison.Ordinal) ||
                                !string.Equals(DateGroups[i][j].Url, groups[i][j].Url, StringComparison.Ordinal))
                            {
                                isIdentical = false;
                                break;
                            }
                        }
                        if (!isIdentical) break;
                    }
                }

                if (!isIdentical)
                {
                    DateGroups.Clear();
                    foreach (var g in groups)
                    {
                        DateGroups.Add(g);
                    }
                }

                IsEmptyStateVisible = groups.Count == 0;
                IsLoadingMore = false;
                UpdatePaginationState();
            });

            // Track impressions off the UI thread
            foreach (var id in sponsoredIds)
            {
                _apiClient.TrackArticleImpressionAsync(id).SafeFireAndForget(_logger, "TrackImpression");
            }
        }).SafeFireAndForget(_logger, "RefreshDisplayedStories");
    }

    private void AppendDisplayedStories(int startIndex, int targetCount)
    {
        var arrangedFeed = _arrangedFeed;

        Task.Run(() =>
        {
            var nextItems = arrangedFeed.Skip(startIndex).Take(targetCount - startIndex).ToList();
            if (nextItems.Count == 0) return;

            // Collect sponsored article IDs for impression tracking
            var sponsoredIds = nextItems
                .Where(s => s.IsSponsored && !s.IsAdMobPlaceholder && !string.IsNullOrWhiteSpace(s.Id))
                .Select(s => s.Id)
                .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var item in nextItems)
                {
                    var itemDate = (item.PublishedAt?.ToLocalTime().Date) ?? DateTime.Today;
                    var existingGroup = DateGroups.FirstOrDefault(g => g.Date == itemDate);

                    if (existingGroup != null)
                    {
                        existingGroup.Add(item);
                    }
                    else
                    {
                        var (header, relative) = itemDate switch
                        {
                            _ when itemDate == DateTime.Today             => ($"Today · {itemDate:dddd, MMMM d, yyyy}", "Today"),
                            _ when itemDate == DateTime.Today.AddDays(-1) => ($"Yesterday · {itemDate:dddd, MMMM d, yyyy}", "Yesterday"),
                            _                                             => (itemDate.ToString("dddd, MMMM d, yyyy"), itemDate.ToString("MMM d"))
                        };

                        var newGroup = new BriefingDateGroup([item])
                        {
                            Date          = itemDate,
                            DateHeader    = header,
                            RelativeLabel = relative
                        };

                        DateGroups.Add(newGroup);
                    }
                }

                UpdatePaginationState();
            });

            foreach (var id in sponsoredIds)
            {
                _apiClient.TrackArticleImpressionAsync(id).SafeFireAndForget(_logger, "TrackImpression");
            }
        }).SafeFireAndForget(_logger, "AppendDisplayedStories");
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

    private void UpdateTimestampStatus(bool isUserInitiated = false)
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

        // Only announce to screen reader on user-initiated actions (pull-to-refresh, pill tap)
        // to avoid overly chatty accessibility noise from background refreshes.
        if (isUserInitiated)
        {
            SemanticScreenReader.Announce(StatusText);
        }
    }

    private void OnBookmarksChanged()
    {
        // Individual ArticleCardView instances listen and update their own bookmark icon state in-place
        // without tearing down and rebuilding the entire CollectionView.
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
        _logger.LogDebug("MainViewModel.ResolveApiBaseUrlAsync START");
        if (!string.IsNullOrWhiteSpace(_resolvedApiBaseUrl))
        {
            return _resolvedApiBaseUrl;
        }

        var saved = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, string.Empty);
        var probe = _httpClientFactory.CreateClient("ProbeClient");

        // Fast path: if we already have a working URL, verify it quickly (1.5s timeout)
        // and return immediately on success. Only fall through to the full probe waterfall
        // if the saved URL fails — avoids the 8-10s probe delay on every pull-to-refresh.
        if (!string.IsNullOrWhiteSpace(saved))
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                _resolvedApiBaseUrl = saved;
                return saved;
            }

            try
            {
                using var quickCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                quickCts.CancelAfter(TimeSpan.FromMilliseconds(1500));
                var resp = await probe.GetAsync($"{saved.TrimEnd('/')}/healthz/liveness", quickCts.Token);
                if (resp.IsSuccessStatusCode)
                {
                    _resolvedApiBaseUrl = saved;
                    return saved;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException { CancellationToken.IsCancellationRequested: true } oce || oce.CancellationToken == ct)
            {
                _logger.LogDebug("Health probe failed for saved URL {Url}: {Error}", saved, ex.Message);
            }
            Preferences.Remove(AppPreferenceKeys.ApiBaseUrl);
        }

        // If offline and no working saved URL, return the platform default without probing
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            var offlineFallback = AppNetworkConstants.DefaultLocalApiUrl;
            _resolvedApiBaseUrl = offlineFallback;
            return offlineFallback;
        }

        string[] candidates = DeviceInfo.Platform == DevicePlatform.Android
            ? [
                "http://10.0.2.2:56193",
                "http://localhost:56193",
                "http://127.0.0.1:56193",
                "http://10.0.2.2:5000",
                "http://localhost:5000",
                "http://host.docker.internal:56193",
                "http://10.0.2.2:8080",
                AppNetworkConstants.ProductionApiUrl
              ]
            : [
                "http://localhost:56193",
                "http://127.0.0.1:56193",
                "http://localhost:5000",
                "http://127.0.0.1:5000",
                "http://host.docker.internal:56193",
                "http://localhost:8080",
                AppNetworkConstants.ProductionApiUrl
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
            _resolvedApiBaseUrl = resolved;
            return resolved;
        }

        var fallback = AppNetworkConstants.DefaultLocalApiUrl;
        Preferences.Set(AppPreferenceKeys.ApiBaseUrl, fallback);
        _resolvedApiBaseUrl = fallback;
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
            _apiClient.TrackArticleClickAsync(articleId).SafeFireAndForget(_logger, "TrackArticleClick");
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

    public async Task LoadTrendingStoriesAsync(CancellationToken ct = default)
    {
        try
        {
            var stories = await _apiClient.GetTrendingStoriesAsync(limit: 6, windowHours: 72, cancellationToken: ct);
            if (stories.Count == 0 && _allCategories.Count > 0)
            {
                stories = BuildFallbackTrendingStories(_allCategories.SelectMany(c => c.Top).ToList());
            }

            if (stories.Count > 0)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TrendingStories = new ObservableCollection<TrendingStoryItem>(stories);
                    HasTrendingStories = true;
                });
            }
            else
            {
                EnsureFallbackTrendingStories(_allCategories);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to load trending stories in mobile view model; using local fallback.");
            EnsureFallbackTrendingStories(_allCategories);
        }
    }

    private void EnsureFallbackTrendingStories(IReadOnlyList<BriefingCategory> categories)
    {
        if (TrendingStories.Count > 0 && HasTrendingStories)
            return;

        var rawItems = categories?.SelectMany(c => c.Top).ToList() ?? [];
        var fallback = BuildFallbackTrendingStories(rawItems);
        if (fallback.Count == 0)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (TrendingStories.Count == 0)
            {
                TrendingStories = new ObservableCollection<TrendingStoryItem>(fallback);
                HasTrendingStories = true;
            }
        });
    }

    public static List<TrendingStoryItem> BuildFallbackTrendingStories(IReadOnlyList<BriefingItem> articles, int limit = 6)
    {
        if (articles == null || articles.Count == 0)
            return [];

        var groups = articles
            .Where(a => !a.IsAdMobPlaceholder && !string.IsNullOrWhiteSpace(a.Title))
            .GroupBy(
                a => string.IsNullOrWhiteSpace(a.Category) ? "General" : a.Category.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Select(a => a.Source).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count())
            .ThenByDescending(g => g.Count())
            .Take(limit)
            .ToList();

        var result = new List<TrendingStoryItem>();
        int rank = 0;

        foreach (var group in groups)
        {
            var topArticles = group.OrderByDescending(a => a.PublishedAt ?? DateTime.MinValue).Take(5).ToList();
            if (topArticles.Count == 0) continue;

            var lead = topArticles[0];
            var sources = topArticles
                .Select(a => a.Source)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(s => s!)
                .ToList();
            if (sources.Count == 0) sources.Add("News Stand NG");

            var catSlug = group.Key.ToLowerInvariant().Replace(" ", "-");
            var now = DateTime.UtcNow;

            result.Add(new TrendingStoryItem
            {
                Id = $"trend-{catSlug}",
                Slug = $"trend-{catSlug}",
                Title = lead.Title,
                Summary = lead.Summary,
                ImageUrl = lead.ImageUrl,
                Category = group.Key,
                ArticleCount = Math.Max(2, topArticles.Count),
                SourceCount = Math.Max(2, sources.Count),
                Sources = sources,
                TrendingScore = Math.Round(18.0 - (rank * 1.5), 2),
                CreatedAt = topArticles.Min(a => a.PublishedAt ?? now.AddHours(-4)),
                UpdatedAt = topArticles.Max(a => a.PublishedAt ?? now),
                Topics = [group.Key]
            });
            rank++;
        }

        return result;
    }

    [RelayCommand]
    private async Task OpenTrendingStoryAsync(TrendingStoryItem? story)
    {
        if (story == null) return;
        await _sonicService.PlaySoundAsync(SonicSound.CategorySelect);
        try
        {
            await Shell.Current.GoToAsync("storycluster", new Dictionary<string, object>
            {
                ["Story"] = story
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to navigate to native storycluster page; falling back to discover search.");
            var query = DiscoverViewModel.GetSearchQueryForStory(story);
            try
            {
                await Shell.Current.GoToAsync($"//discover?query={Uri.EscapeDataString(query)}");
            }
            catch (Exception innerEx)
            {
                _logger.LogWarning(innerEx, "Fallback discover navigation also failed.");
            }
        }
    }

    [RelayCommand]
    private void ToggleNairaFxExpanded()
    {
        IsNairaFxExpanded = !IsNairaFxExpanded;
    }

    private void ResetNairaFxToOfflineDefaults()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            NairaFxPillText = OfflineNairaFxPillText;
            NairaFxParallelSummary = OfflineNairaFxParallelSummary;
            NairaFxOfficialSummary = OfflineNairaFxOfficialSummary;
            HasNairaFxData = true;
        });
    }

    public async Task LoadNairaFxSummaryAsync(CancellationToken ct = default)
    {
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                ResetNairaFxToOfflineDefaults();
                return;
            }

            var metric = await _apiClient.GetMarketMetricAsync("naira-fx", ct);
            if (metric == null || string.IsNullOrWhiteSpace(metric.PrimaryValue))
            {
                ResetNairaFxToOfflineDefaults();
                return;
            }

            var pillText = metric.PrimaryValue;
            var officialText = !string.IsNullOrWhiteSpace(metric.SecondaryValue)
                ? metric.SecondaryValue
                : OfflineNairaFxOfficialSummary;

            string parallelSummary = $"Parallel: {metric.PrimaryValue}";
            if (metric.Currencies is { Count: > 0 } currencies)
            {
                var parts = new List<string>();
                if (currencies.TryGetValue("USD", out var usd))
                {
                    if (usd.ParallelSell > 0)
                        pillText = $"₦{usd.ParallelSell:N0}/$";
                    parts.Add($"$ ₦{(usd.ParallelSell > 0 ? usd.ParallelSell : 0):N0}");
                }
                if (currencies.TryGetValue("GBP", out var gbp))
                    parts.Add($"£ ₦{(gbp.ParallelSell > 0 ? gbp.ParallelSell : 0):N0}");
                if (currencies.TryGetValue("EUR", out var eur))
                    parts.Add($"€ ₦{(eur.ParallelSell > 0 ? eur.ParallelSell : 0):N0}");

                if (parts.Count > 0)
                {
                    parallelSummary = "Parallel: " + string.Join(" · ", parts);
                }
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                NairaFxPillText = pillText;
                NairaFxParallelSummary = parallelSummary;
                NairaFxOfficialSummary = officialText;
                HasNairaFxData = true;
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Naira FX summary load failed or offline; resetting to 0 defaults.");
            ResetNairaFxToOfflineDefaults();
        }
    }

    public async Task LoadSponsoredCampaignsAsync(CancellationToken ct = default)
    {
        try
        {
            var sponsored = await _apiClient.GetSponsoredArticlesAsync(platform: "Mobile", cancellationToken: ct);
            var hero = sponsored.FirstOrDefault(s => string.Equals(s.PlacementZone, "MainPage_HeroSpotlight", StringComparison.OrdinalIgnoreCase));
            var shelf = sponsored.Where(s => string.Equals(s.PlacementZone, "MainPage_PartnerShelf", StringComparison.OrdinalIgnoreCase)).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                HeroSpotlightCampaign = hero;
                HasHeroSpotlight = hero != null;
                PartnerShelfCampaigns = new ObservableCollection<BriefingItem>(shelf);
                HasPartnerShelf = shelf.Count > 0;
            });

            if (hero != null && !string.IsNullOrWhiteSpace(hero.Id))
            {
                _apiClient.TrackArticleImpressionAsync(hero.Id).SafeFireAndForget(_logger, "TrackHeroImpression");
            }
            foreach (var item in shelf)
            {
                if (!string.IsNullOrWhiteSpace(item.Id))
                {
                    _apiClient.TrackArticleImpressionAsync(item.Id).SafeFireAndForget(_logger, "TrackShelfImpression");
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to load mobile sponsored campaigns.");
        }
    }

    [RelayCommand]
    public async Task OpenSponsoredHeroAsync()
    {
        if (HeroSpotlightCampaign != null)
        {
            await _interactionService.OpenArticleAsync(HeroSpotlightCampaign);
        }
    }

    [RelayCommand]
    public async Task OpenPartnerShelfStoryAsync(BriefingItem? item)
    {
        if (item != null)
        {
            await _interactionService.OpenArticleAsync(item);
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
        if (e.NetworkAccess != NetworkAccess.Internet)
        {
            ResetNairaFxToOfflineDefaults();
        }
        else
        {
            LoadNairaFxSummaryAsync(CancellationToken.None).SafeFireAndForget(_logger, "LoadNairaFxSummaryOnReconnect");
        }

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
        _audioCoordinator.StateChanged -= OnAudioCoordinatorStateChanged;
        _audioCoordinator.Dispose();
        _bookmarkService.BookmarksChanged -= OnBookmarkServiceChanged;
        _eventBus.PlayAudioBriefingRequested -= OnPlayAudioBriefingRequested;
        _eventBus.NewStoriesAvailable -= OnNewStoriesAvailable;
        _eventBus.AppResumed -= OnAppResumedFromBridge;
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }

}
