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
    private readonly List<BriefingItem> _bookmarks = [];
    private List<BriefingCategory> _allCategories = [];
    private CancellationTokenSource? _loadCts;

    private const int PageSize = 15;
    private int _displayedCount = 15;
    private int _totalAvailableStories = 0;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public MainPage(
        NewsApiClient apiClient, 
        IHttpClientFactory httpClientFactory,
        IKeywordMatchingService keywordMatchingService,
        INotificationService notificationService)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _httpClientFactory = httpClientFactory;
        _keywordMatchingService = keywordMatchingService;
        _notificationService = notificationService;

        Loaded += async (_, _) =>
        {
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

    // ──────────────────────────────────────────────────────────
    // Data Loading
    // ──────────────────────────────────────────────────────────

    private async Task LoadBriefingAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        try
        {
            var endpoint = await ResolveApiBaseUrlAsync(ct);
            _apiClient.BaseUrl = endpoint;

            var language = Preferences.Get("preferred_language", "English");
            SetStatusLoading(language);

            var categories = await _apiClient.GetDailyBriefingAsync(language, ct);
            if (categories.Count == 0)
            {
                var cachedJson = Preferences.Get("last_briefing", string.Empty);
                if (!string.IsNullOrWhiteSpace(cachedJson))
                {
                    try
                    {
                        categories = JsonSerializer.Deserialize<List<BriefingCategory>>(cachedJson, JsonOptions) ?? [];
                    }
                    catch (JsonException ex)
                    {
                        Debug.WriteLine($"Cache deserialization failed: {ex.Message}");
                        categories = GetFallbackSampleBriefing(language);
                    }
                }
                else
                {
                    categories = GetFallbackSampleBriefing(language);
                }
            }
            else
            {
                Preferences.Set("last_briefing", JsonSerializer.Serialize(categories, JsonOptions));
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
                NotificationPreferences.MonitoredKeywords);

            foreach (var match in matches)
            {
                NotificationPreferences.RecordNotifiedArticle(match.Article.Id);
                _notificationService.ShowKeywordAlertNotification(
                    match.MatchedKeyword,
                    match.Article.Title,
                    match.Article.Id);
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
        var audioUrl = $"{_apiClient.BaseUrl.TrimEnd('/')}/api/v1/briefings/audio";
        if (Uri.TryCreate(audioUrl, UriKind.Absolute, out var uri))
        {
            SemanticScreenReader.Announce("Opening daily audio briefing…");
            await Launcher.OpenAsync(uri);
        }
        else
        {
            await DisplayAlertAsync("Daily Audio Briefing",
                "The AI audio pipeline synthesizes top headlines into a unified track. Ensure the TTS service is running.",
                "OK");
        }
    }

    private void OnBookmarkClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: BriefingItem item }) return;

        string message;
        if (_bookmarks.Any(b => b.Id == item.Id))
        {
            _bookmarks.RemoveAll(b => b.Id == item.Id);
            message = "Bookmark removed.";
        }
        else
        {
            _bookmarks.Add(item);
            message = $"Saved: {item.Title}";
        }

        Preferences.Set("bookmarks", JsonSerializer.Serialize(_bookmarks, JsonOptions));
        SemanticScreenReader.Announce(message);
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        BriefingItem? item = null;
        if (sender is Button { CommandParameter: BriefingItem btnItem }) item = btnItem;
        else if (sender is BindableObject { BindingContext: BriefingItem bItem }) item = bItem;
        if (item is null) return;

        var shareText = $"Check out this story from {item.Source ?? "Nigerian News Grid"}:\n\n{item.Title}\n\n{item.Summary}\n\nRead more: {item.Url}".Trim();
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
        var cached = Preferences.Get("bookmarks", string.Empty);
        if (string.IsNullOrWhiteSpace(cached)) return;
        try
        {
            var items = JsonSerializer.Deserialize<List<BriefingItem>>(cached, JsonOptions);
            if (items is null) return;
            _bookmarks.Clear();
            _bookmarks.AddRange(items);
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"Bookmark restore failed: {ex.Message}");
            Preferences.Remove("bookmarks");
        }
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

    // ──────────────────────────────────────────────────────────
    // Fallback Data
    // ──────────────────────────────────────────────────────────

    private static List<BriefingCategory> GetFallbackSampleBriefing(string language)
    {

        var imageByCategory = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Politics"] = "https://images.unsplash.com/photo-1540910419892-4a36d2c3266c?w=600&auto=format&fit=crop&q=80",
            ["Business"] = "https://images.unsplash.com/photo-1611974789855-9c2a0a7236a3?w=600&auto=format&fit=crop&q=80",
            ["Sports"] = "https://images.unsplash.com/photo-1508098682722-e99c43a406b2?w=600&auto=format&fit=crop&q=80",
            ["Technology"] = "https://images.unsplash.com/photo-1518770660439-4636190af475?w=600&auto=format&fit=crop&q=80",
            ["Entertainment"] = "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=600&auto=format&fit=crop&q=80",
        };

        BriefingItem MakeItem(string title, string summary, string url, string source, string category) =>
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Title = title,
                Summary = summary,
                Url = url,
                Source = source,
                Category = category,
                ImageUrl = imageByCategory.GetValueOrDefault(category,
                    "https://images.unsplash.com/photo-1585829365295-ab7cd400c167?w=600&auto=format&fit=crop&q=80"),
                PublishedAt = DateTime.UtcNow.AddHours(-new Random().Next(1, 12)),
                VoiceName = $"{language} Female"
            };

        return
        [
            new BriefingCategory
            {
                Category = "Politics",
                Top =
                [
                    MakeItem(
                        "Federal Government Unveils New Digital Economy Roadmap",
                        "The Ministry of Communications and Digital Economy announced a strategic initiative targeting infrastructure expansion, broadband coverage, and youth technical skill development nationwide.",
                        "https://punchng.com/news/digital-roadmap",
                        "Punch Newspaper",
                        "Politics"),
                    MakeItem(
                        "National Assembly Passes Key Energy & Power Sector Reform Bill",
                        "Lawmakers approved comprehensive legislative measures to enhance power grid reliability and boost renewable energy investments across state governments.",
                        "https://guardian.ng/news/energy-bill-passed",
                        "The Guardian Nigeria",
                        "Politics")
                ]
            },
            new BriefingCategory
            {
                Category = "Sports",
                Top =
                [
                    MakeItem(
                        "Super Eagles Prepare for International Friendly Match",
                        "Coaching staff confirmed full squad arrival at camp ahead of weekend international clash, highlighting tactical adjustments and player fitness.",
                        "https://guardian.ng/sports/super-eagles-friendly",
                        "The Guardian Nigeria",
                        "Sports")
                ]
            },
            new BriefingCategory
            {
                Category = "Business",
                Top =
                [
                    MakeItem(
                        "Central Bank Highlights Monetary Policy & Foreign Exchange Outlook",
                        "Key financial indicators show steady stabilization across foreign exchange markets, trade balances, and agricultural sector loans.",
                        "https://www.premiumtimesng.com/business/cbn-monetary-policy",
                        "Premium Times",
                        "Business")
                ]
            },
            new BriefingCategory
            {
                Category = "Technology",
                Top =
                [
                    MakeItem(
                        "Tech Hub Ecosystem Expands Across Lagos, Abuja and Port Harcourt",
                        "Venture capital investments in Nigerian fintech and artificial intelligence startups reached new record milestones this quarter.",
                        "https://punchng.com/tech/startup-growth",
                        "Punch Newspaper",
                        "Technology")
                ]
            }
        ];
    }
}
