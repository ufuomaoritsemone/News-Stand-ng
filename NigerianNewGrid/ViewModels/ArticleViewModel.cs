using System.Collections.ObjectModel;
using System.Text.Json;
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
/// ViewModel for ArticleWebPage (Issue #19).
/// Encapsulates article state, content hydration, typography settings, reader mode, bookmarking, and related stories.
/// </summary>
public partial class ArticleViewModel : ObservableObject
{
    private readonly NewsApiClient _apiClient;
    private readonly IStoryRepository _storyRepository;
    private readonly IBookmarkService _bookmarkService;
    private readonly IRecentlyReadService _recentlyReadService;
    private readonly IAnalyticsService _analyticsService;
    private readonly IXShareService _xShareService;
    private readonly ILogger<ArticleViewModel> _logger;

    private static JsonSerializerOptions JsonOptions => SharedJsonOptions.Default;

    [ObservableProperty] public partial string ArticleUrl { get; set; } = string.Empty;
    [ObservableProperty] public partial string? Title { get; set; }
    [ObservableProperty] public partial string? ImageUrl { get; set; }
    [ObservableProperty] public partial string? Category { get; set; }
    [ObservableProperty] public partial string? ArticleId { get; set; }
    [ObservableProperty] public partial string? Content { get; set; }
    [ObservableProperty] public partial string? Summary { get; set; }
    [ObservableProperty] public partial string? Author { get; set; }
    [ObservableProperty] public partial string? ContentType { get; set; }
    [ObservableProperty] public partial string? Slug { get; set; }
    [ObservableProperty] public partial bool IsSponsored { get; set; }
    [ObservableProperty] public partial string? SponsorName { get; set; }
    [ObservableProperty] public partial bool IsVideoStory { get; set; }
    [ObservableProperty] public partial bool IsReaderMode { get; set; } = true;
    [ObservableProperty] public partial bool IsWebViewVisible { get; set; }

    partial void OnIsReaderModeChanged(bool value)
    {
        IsWebViewVisible = !value;
        ModeToggleText = value ? "📖 Reader" : "🌐 Web";
    }

    [ObservableProperty] public partial int FontSizeLevel { get; set; } = 1; // 0 = 15pt, 1 = 17pt, 2 = 20pt, 3 = 24pt
    [ObservableProperty] public partial ArticleDistillerService.PublisherInfo Publisher { get; set; } = new("News", null, "#1B3B6F", "#0A192F");

    [ObservableProperty] public partial string ReadingTimeLabel { get; set; } = "2 min read";
    [ObservableProperty] public partial string DisplaySponsor { get; set; } = string.Empty;
    [ObservableProperty] public partial string PublisherDomain { get; set; } = "news";
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial bool HasNoContent { get; set; }
    [ObservableProperty] public partial string FontSizeButtonLabel { get; set; } = "A";

    [ObservableProperty] public partial bool IsBookmarked { get; set; }
    [ObservableProperty] public partial bool IsRelatedExpanded { get; set; }
    [ObservableProperty] public partial bool HasHeroImage { get; set; }
    [ObservableProperty] public partial bool HasSummary { get; set; }
    [ObservableProperty] public partial string PublisherAttribution { get; set; } = string.Empty;
    [ObservableProperty] public partial string CategoryBadgeText { get; set; } = "NEWS";
    [ObservableProperty] public partial string HeaderPublisherText { get; set; } = "Newspaper";
    [ObservableProperty] public partial ObservableCollection<string> Paragraphs { get; set; } = [];
    [ObservableProperty] public partial double CurrentFontSize { get; set; } = 17.0;
    [ObservableProperty] public partial double CurrentLineHeight { get; set; } = 1.4;
    [ObservableProperty] public partial bool IsBottomBannerVisible { get; set; } = true;
    [ObservableProperty] public partial string ModeToggleText { get; set; } = "📖 Reader";
    [ObservableProperty] public partial bool IsRelatedCollapsedVisible { get; set; }
    [ObservableProperty] public partial bool HasRelatedArticles { get; set; }
    [ObservableProperty] public partial bool HasRelatedVideos { get; set; }

    // AI Executive Briefing / Quick Read State
    [ObservableProperty] public partial bool HasAiSummary { get; set; }
    [ObservableProperty] public partial string? AiSummary { get; set; }
    [ObservableProperty] public partial string? AiSummaryEngine { get; set; }
    [ObservableProperty] public partial ObservableCollection<string> KeyTakeaways { get; set; } = [];
    [ObservableProperty] public partial bool HasKeyTakeaways { get; set; }
    [ObservableProperty] public partial bool IsAiSummaryExpanded { get; set; } = true;
    [ObservableProperty] public partial string AiSummaryToggleIcon { get; set; } = "▲";
    [ObservableProperty] public partial string AiEngineBadgeText { get; set; } = "⚡ AI Quick Read";
    [ObservableProperty] public partial int CommentCount { get; set; }

    // Related stories
    [ObservableProperty] public partial ObservableCollection<RelatedStoryDisplayItem> DisplayedRelatedItems { get; set; } = [];
    [ObservableProperty] public partial string SelectedFilter { get; set; } = "All";
    [ObservableProperty] public partial int TotalRelatedCount { get; set; }
    [ObservableProperty] public partial int ArticlesRelatedCount { get; set; }
    [ObservableProperty] public partial int VideosRelatedCount { get; set; }
    [ObservableProperty] public partial string RelatedCollapsedText { get; set; } = string.Empty;

    // In-Article Sponsorship Placements
    [ObservableProperty] public partial BriefingItem? HeaderSponsor { get; set; }
    [ObservableProperty] public partial bool HasHeaderSponsor { get; set; }
    [ObservableProperty] public partial BriefingItem? MidContentSponsor { get; set; }
    [ObservableProperty] public partial bool HasMidContentSponsor { get; set; }
    [ObservableProperty] public partial BriefingItem? FooterSponsor { get; set; }
    [ObservableProperty] public partial bool HasFooterSponsor { get; set; }

    public List<RelatedStoryDisplayItem> AllRelatedItems { get; private set; } = [];

    public event Action? StoryDataApplied;
    public event Action? FontSizeChanged;

    public ArticleViewModel(
        NewsApiClient apiClient,
        IStoryRepository storyRepository,
        IBookmarkService bookmarkService,
        IRecentlyReadService recentlyReadService,
        IAnalyticsService analyticsService,
        IXShareService xShareService,
        ILogger<ArticleViewModel> logger)
    {
        _apiClient = apiClient;
        _storyRepository = storyRepository;
        _bookmarkService = bookmarkService;
        _recentlyReadService = recentlyReadService;
        _analyticsService = analyticsService;
        _xShareService = xShareService;
        _logger = logger;
    }

    public void Initialize(ArticleNavigationParams parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        Initialize(
            url: parameters.Url,
            title: parameters.Title,
            imageUrl: parameters.ImageUrl,
            category: parameters.Category,
            articleId: parameters.ArticleId,
            isSponsored: parameters.IsSponsored,
            sponsorName: parameters.SponsorName,
            content: parameters.Content,
            summary: parameters.Summary,
            author: parameters.Author,
            contentType: parameters.ContentType,
            hasAiSummary: parameters.HasAiSummary,
            aiSummary: parameters.AiSummary,
            aiSummaryEngine: parameters.AiSummaryEngine,
            keyTakeaways: parameters.KeyTakeaways,
            slug: parameters.Slug);
    }

    public void Initialize(
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
        string? contentType = null,
        bool hasAiSummary = false,
        string? aiSummary = null,
        string? aiSummaryEngine = null,
        IReadOnlyList<string>? keyTakeaways = null,
        int commentCount = 0,
        string? slug = null)
    {
        ArticleUrl = url ?? string.Empty;
        Title = title;
        ImageUrl = imageUrl;
        Category = category;
        ArticleId = articleId;
        Slug = slug;
        Content = content;
        Summary = summary;
        Author = author;
        ContentType = contentType;
        IsSponsored = isSponsored;
        SponsorName = sponsorName;

        HasAiSummary = hasAiSummary || !string.IsNullOrWhiteSpace(aiSummary);
        AiSummary = aiSummary;
        AiSummaryEngine = aiSummaryEngine;
        CommentCount = commentCount;

        KeyTakeaways.Clear();
        if (keyTakeaways != null)
        {
            foreach (var k in keyTakeaways) KeyTakeaways.Add(k);
        }
        HasKeyTakeaways = KeyTakeaways.Count > 0;

        IsVideoStory = string.Equals(category, "Video", StringComparison.OrdinalIgnoreCase) ||
                       (!string.IsNullOrWhiteSpace(url) && (url.Contains("youtube.com") || url.Contains("youtu.be")));

        Publisher = ArticleDistillerService.IdentifyPublisher(ArticleUrl);
        PublisherDomain = GetPublisherDomain(ArticleUrl);

        var (_, readTime) = ArticleDistillerService.CalculateReadingTime(!string.IsNullOrWhiteSpace(Content) ? Content : Title);
        ReadingTimeLabel = readTime;
        DisplaySponsor = !string.IsNullOrWhiteSpace(sponsorName) ? sponsorName : Publisher.Name;

        IsReaderMode = !IsVideoStory;
        HasNoContent = string.IsNullOrWhiteSpace(Content) && string.IsNullOrWhiteSpace(ArticleId) && !HasAiSummary && string.IsNullOrWhiteSpace(Summary);

        RecordRead();
        RefreshUiBindings();

        if (!string.IsNullOrWhiteSpace(ArticleId))
        {
            HydrateContentIfMissingAsync(ArticleId).SafeFireAndForget(_logger, "HydrateContent");
        }

        LoadRelatedStoriesAsync().SafeFireAndForget(_logger, "LoadRelatedStories");
        LoadInArticleSponsorsAsync().SafeFireAndForget(_logger, "LoadInArticleSponsors");
    }

    public async Task HydrateContentIfMissingAsync(string? articleId)
    {
        if (string.IsNullOrWhiteSpace(articleId))
        {
            HasNoContent = string.IsNullOrWhiteSpace(Content);
            return;
        }

        try
        {
            IsLoading = true;

            // 1. Check local SQLite cache first (instant, 0KB network, works offline)
            var cachedStory = await _storyRepository.GetStoryByIdAsync(articleId);
            if (cachedStory != null && !string.IsNullOrWhiteSpace(cachedStory.Content))
            {
                ApplyStoryData(cachedStory);
                return;
            }

            // 2. Fetch from backend API by Guid if missing or incomplete in local cache
            var article = await _apiClient.GetArticleByIdAsync(articleId);
            if (article != null)
            {
                _storyRepository.SaveStoryAsync(article).SafeFireAndForget(_logger, "SaveHydratedArticle");

                if (!string.IsNullOrWhiteSpace(article.Content))
                {
                    ApplyStoryData(article);
                    return;
                }
            }

            HasNoContent = string.IsNullOrWhiteSpace(Content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to hydrate article content for ID {ArticleId}", articleId);
            HasNoContent = string.IsNullOrWhiteSpace(Content);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ApplyStoryData(BriefingItem story)
    {
        Content = story.Content;
        if (string.IsNullOrWhiteSpace(Summary)) Summary = story.Summary;
        if (string.IsNullOrWhiteSpace(Title)) Title = story.Title;
        if (string.IsNullOrWhiteSpace(ArticleUrl) && !string.IsNullOrWhiteSpace(story.Url)) ArticleUrl = story.Url;
        if (string.IsNullOrWhiteSpace(ImageUrl) && !string.IsNullOrWhiteSpace(story.ImageUrl)) ImageUrl = story.ImageUrl;
        if (string.IsNullOrWhiteSpace(Author) && !string.IsNullOrWhiteSpace(story.Author)) Author = story.Author;
        if (string.IsNullOrWhiteSpace(Category) && !string.IsNullOrWhiteSpace(story.Category)) Category = story.Category;
        if (!string.IsNullOrWhiteSpace(story.Slug)) Slug = story.Slug;

        if (story.HasAiSummary || !string.IsNullOrWhiteSpace(story.AiSummary))
        {
            HasAiSummary = true;
            AiSummary = story.AiSummary ?? AiSummary;
            AiSummaryEngine = story.AiSummaryEngine ?? AiSummaryEngine;
        }

        if (story.KeyTakeaways.Count > 0)
        {
            KeyTakeaways.Clear();
            foreach (var k in story.KeyTakeaways) KeyTakeaways.Add(k);
            HasKeyTakeaways = true;
        }

        if (story.CommentCount > 0)
        {
            CommentCount = story.CommentCount;
        }

        if (!string.IsNullOrWhiteSpace(ArticleUrl))
        {
            Publisher = ArticleDistillerService.IdentifyPublisher(ArticleUrl);
            PublisherDomain = GetPublisherDomain(ArticleUrl);
        }

        var (_, readTime) = ArticleDistillerService.CalculateReadingTime(Content);
        ReadingTimeLabel = readTime;
        HasNoContent = string.IsNullOrWhiteSpace(Content) && !HasAiSummary && string.IsNullOrWhiteSpace(Summary);

        RecordRead();
        RefreshUiBindings();
        StoryDataApplied?.Invoke();
    }

    public void RecordRead()
    {
        if (!string.IsNullOrWhiteSpace(ArticleUrl))
        {
            _recentlyReadService.RecordRead(ArticleUrl, Title, ImageUrl, Category, ArticleId);
        }
        _analyticsService.TrackAsync("article_read", ArticleId, Title, Category).SafeFireAndForget(_logger, "TrackArticleRead");
    }

    [RelayCommand]
    public async Task<bool> ToggleBookmarkAsync()
    {
        if (string.IsNullOrWhiteSpace(ArticleUrl)) return false;

        var item = new BriefingItem
        {
            Id = ArticleId ?? Guid.NewGuid().ToString("N"),
            Title = Title ?? Publisher.Name,
            Url = ArticleUrl,
            ImageUrl = ImageUrl ?? string.Empty,
            Category = Category ?? "News",
            Source = Publisher.Name,
            PublishedAt = DateTime.UtcNow,
            Content = Content,
            Summary = Summary,
            Slug = Slug,
            HasAiSummary = HasAiSummary,
            AiSummary = AiSummary,
            AiSummaryEngine = AiSummaryEngine,
            KeyTakeaways = KeyTakeaways.ToList(),
            CommentCount = CommentCount
        };

        bool added = _bookmarkService.ToggleBookmark(item);
        IsBookmarked = added;
        string msg = added ? $"Saved: {item.Title}" : "Bookmark removed.";
        SemanticScreenReader.Announce(msg);

        if (added)
        {
            _analyticsService.TrackAsync("article_bookmark", ArticleId, Title, Category).SafeFireAndForget(_logger, "TrackBookmark");
        }
        return added;
    }

    [RelayCommand]
    public async Task ShareAsync()
    {
        if (string.IsNullOrWhiteSpace(ArticleUrl) && string.IsNullOrWhiteSpace(Slug) && string.IsNullOrWhiteSpace(ArticleId)) return;

        _analyticsService.TrackAsync("article_share", ArticleId, Title, Category).SafeFireAndForget(_logger, "TrackShare");

        var newsstandUrl = NewsStandUrlHelper.GetArticleWebUrl(Slug, ArticleId, Title, ArticleUrl);
        var shareText = $"{Title ?? "Check out this story"}\n\nRead more on News Stand NG: {newsstandUrl}\n\n📲 Follow news as it breaks — download News Stand NG and never miss a story.";
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = Title ?? "Share Article",
            Uri = newsstandUrl,
            Text = shareText
        });
    }

    [RelayCommand]
    public async Task ShareOnXAsync()
    {
        _analyticsService.TrackAsync("article_share_x", ArticleId, Title, Category).SafeFireAndForget(_logger, "TrackShareX");

        await _xShareService.ShareStoryOnXAsync(
            title: Title,
            source: Publisher?.Name,
            storyUrl: ArticleUrl,
            articleId: ArticleId,
            slug: Slug);
    }

    [RelayCommand]
    public async Task ShareOnWhatsAppAsync()
    {
        if (string.IsNullOrWhiteSpace(ArticleUrl) && string.IsNullOrWhiteSpace(Slug) && string.IsNullOrWhiteSpace(ArticleId)) return;

        _analyticsService.TrackAsync("article_share_whatsapp", ArticleId, Title, Category).SafeFireAndForget(_logger, "TrackShareWhatsApp");

        var newsstandUrl = NewsStandUrlHelper.GetArticleWebUrl(Slug, ArticleId, Title, ArticleUrl);
        var preview = !string.IsNullOrWhiteSpace(AiSummary) ? AiSummary : (!string.IsNullOrWhiteSpace(Summary) ? Summary : string.Empty);
        var text = $"*{Title ?? "News Update"}*\n\n{preview}\n\nRead more on News Stand NG: {newsstandUrl}\n\n📲 *Download News Stand NG* for verified Nigerian headlines.";
        var encoded = Uri.EscapeDataString(text);
        var uri = $"https://wa.me/?text={encoded}";

        try
        {
            await Launcher.Default.OpenAsync(new Uri(uri));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to launch WhatsApp directly, falling back to system share");
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = Title ?? "Share via WhatsApp",
                Uri = newsstandUrl,
                Text = text
            });
        }
    }

    [RelayCommand]
    public void ToggleAiSummaryExpanded()
    {
        IsAiSummaryExpanded = !IsAiSummaryExpanded;
        AiSummaryToggleIcon = IsAiSummaryExpanded ? "▲" : "▼";
    }

    [RelayCommand]
    public void CycleFontSize()
    {
        FontSizeLevel = (FontSizeLevel + 1) % 4;
        FontSizeButtonLabel = FontSizeLevel switch
        {
            0 => "A-",
            1 => "A",
            2 => "A+",
            3 => "A++",
            _ => "A"
        };
        CurrentFontSize = GetCurrentFontSize();
        CurrentLineHeight = GetCurrentLineHeight();
        FontSizeChanged?.Invoke();
    }

    [RelayCommand]
    public void DismissBottomBanner()
    {
        IsBottomBannerVisible = false;
    }

    [RelayCommand]
    public void ToggleReaderMode()
    {
        if (IsVideoStory) return;
        IsReaderMode = !IsReaderMode;
    }

    [RelayCommand]
    public void OpenOriginalWeb()
    {
        IsReaderMode = false;
    }

    [RelayCommand]
    public void ToggleRelatedExpanded()
    {
        IsRelatedExpanded = !IsRelatedExpanded;
        IsRelatedCollapsedVisible = !IsRelatedExpanded && TotalRelatedCount > 0;
    }

    public void RefreshUiBindings()
    {
        HasHeroImage = !string.IsNullOrWhiteSpace(ImageUrl);
        HasSummary = !string.IsNullOrWhiteSpace(Summary);
        HasKeyTakeaways = KeyTakeaways.Count > 0;
        AiEngineBadgeText = !string.IsNullOrWhiteSpace(AiSummaryEngine) && AiSummaryEngine.Contains("gemini", StringComparison.OrdinalIgnoreCase)
            ? "⚡ Gemini 1.5 Flash"
            : "⚡ AI Quick Read";

        bool isOpinion = string.Equals(ContentType, "Opinion", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(Category, "Opinion", StringComparison.OrdinalIgnoreCase);
        CategoryBadgeText = isOpinion ? "✍️ OPINION / EDITORIAL" : (Category ?? "NEWS").ToUpperInvariant();
        PublisherAttribution = !string.IsNullOrWhiteSpace(Author)
            ? $"By {Author} • {Publisher.Name}"
            : Publisher.Name;

        // If AI summary exists, estimate ~45s reading time
        if (HasAiSummary && !string.IsNullOrWhiteSpace(AiSummary))
        {
            ReadingTimeLabel = "~45s quick read";
            HeaderPublisherText = $"{Publisher.Name} · ⚡ 45s read";
        }
        else
        {
            HeaderPublisherText = $"{Publisher.Name} · ⏱️ {ReadingTimeLabel}";
        }

        ModeToggleText = IsReaderMode ? "📖 Reader" : "🌐 Web";

        var paras = !string.IsNullOrWhiteSpace(Content)
            ? Content.Split(["\r\n\r\n", "\n\n", "\r\r"], StringSplitOptions.RemoveEmptyEntries)
            : [];

        Paragraphs.Clear();
        foreach (var p in paras)
        {
            var trimmed = p.Trim();
            if (trimmed.Length > 0) Paragraphs.Add(trimmed);
        }

        CurrentFontSize = GetCurrentFontSize();
        CurrentLineHeight = GetCurrentLineHeight();
        IsRelatedCollapsedVisible = !IsRelatedExpanded && TotalRelatedCount > 0;
        HasRelatedArticles = ArticlesRelatedCount > 0;
        HasRelatedVideos = VideosRelatedCount > 0;
        if (!string.IsNullOrWhiteSpace(ArticleId))
        {
            IsBookmarked = _bookmarkService.IsBookmarked(ArticleId);
        }
    }

    public double GetCurrentFontSize() => FontSizeLevel switch
    {
        0 => 15.0,
        1 => 17.0,
        2 => 20.0,
        3 => 24.0,
        _ => 17.0
    };

    public double GetCurrentLineHeight() => FontSizeLevel switch
    {
        0 => 1.35,
        1 => 1.4,
        2 => 1.45,
        3 => 1.5,
        _ => 1.4
    };

    public string GetFontSizeString() => FontSizeLevel switch
    {
        0 => "15px",
        1 => "17px",
        2 => "20px",
        3 => "24px",
        _ => "17px"
    };

    public async Task LoadRelatedStoriesAsync()
    {
        try
        {
            var result = await _apiClient.GetRelatedStoriesAsync(ArticleId, Title, Category, limit: 4);
            var items = new List<RelatedStoryDisplayItem>();

            if (result.Articles.Count > 0)
            {
                foreach (var art in result.Articles)
                {
                    if (string.Equals(art.Url, ArticleUrl, StringComparison.OrdinalIgnoreCase)) continue;
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

            if (result.Videos.Count > 0)
            {
                foreach (var vid in result.Videos)
                {
                    if (string.Equals(vid.VideoUrl, ArticleUrl, StringComparison.OrdinalIgnoreCase)) continue;
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

            if (items.Count == 0)
            {
                items = await GetLocalFallbackRelatedStoriesAsync(Title, Category);
            }

            AllRelatedItems = items;
            UpdateRelatedSummary();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load remote related stories, attempting fallback.");
            AllRelatedItems = await GetLocalFallbackRelatedStoriesAsync(Title, Category);
            UpdateRelatedSummary();
        }
    }

    public async Task LoadInArticleSponsorsAsync(CancellationToken ct = default)
    {
        try
        {
            var sponsored = await _apiClient.GetSponsoredArticlesAsync(platform: "Mobile", cancellationToken: ct);

            var header = sponsored.FirstOrDefault(s => string.Equals(s.PlacementZone, "InArticle_Header", StringComparison.OrdinalIgnoreCase));
            var mid = sponsored.FirstOrDefault(s => string.Equals(s.PlacementZone, "InArticle_MidContent", StringComparison.OrdinalIgnoreCase));
            var footer = sponsored.FirstOrDefault(s => string.Equals(s.PlacementZone, "InArticle_Footer", StringComparison.OrdinalIgnoreCase));

            MainThread.BeginInvokeOnMainThread(() =>
            {
                HeaderSponsor = header;
                HasHeaderSponsor = header != null;

                MidContentSponsor = mid;
                HasMidContentSponsor = mid != null;

                FooterSponsor = footer;
                HasFooterSponsor = footer != null;
            });

            if (header != null && !string.IsNullOrWhiteSpace(header.Id))
            {
                _apiClient.TrackArticleImpressionAsync(header.Id).SafeFireAndForget(_logger, "TrackHeaderSponsorImpression");
            }
            if (mid != null && !string.IsNullOrWhiteSpace(mid.Id))
            {
                _apiClient.TrackArticleImpressionAsync(mid.Id).SafeFireAndForget(_logger, "TrackMidSponsorImpression");
            }
            if (footer != null && !string.IsNullOrWhiteSpace(footer.Id))
            {
                _apiClient.TrackArticleImpressionAsync(footer.Id).SafeFireAndForget(_logger, "TrackFooterSponsorImpression");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to load in-article sponsors.");
        }
    }

    [RelayCommand]
    public async Task OpenInArticleSponsorAsync(BriefingItem? sponsor)
    {
        if (sponsor == null) return;
        if (!string.IsNullOrWhiteSpace(sponsor.Id))
        {
            _apiClient.TrackArticleClickAsync(sponsor.Id).SafeFireAndForget(_logger, "TrackInArticleSponsorClick");
        }

        var targetUrl = !string.IsNullOrWhiteSpace(sponsor.SponsorUrl) ? sponsor.SponsorUrl.Trim() : sponsor.Url?.Trim();
        if (string.IsNullOrWhiteSpace(targetUrl)) return;

        if (Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            try
            {
                await Launcher.Default.OpenAsync(uri);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to open sponsor URL {Url}", targetUrl);
            }
        }
    }

    [RelayCommand]
    public void SetRelatedFilter(string? filter)
    {
        SelectedFilter = filter ?? "All";
        ApplyRelatedFilter();
    }

    private void UpdateRelatedSummary()
    {
        TotalRelatedCount = AllRelatedItems.Count;
        ArticlesRelatedCount = AllRelatedItems.Count(i => i.Type == "Article");
        VideosRelatedCount = AllRelatedItems.Count(i => i.Type == "Video");
        RelatedCollapsedText = $"✨ Related Stories ({TotalRelatedCount}) · 📰 {ArticlesRelatedCount}  🎬 {VideosRelatedCount}";
        HasRelatedArticles = ArticlesRelatedCount > 0;
        HasRelatedVideos = VideosRelatedCount > 0;
        IsRelatedCollapsedVisible = !IsRelatedExpanded && TotalRelatedCount > 0;

        ApplyRelatedFilter();
    }

    private void ApplyRelatedFilter()
    {
        var filtered = SelectedFilter switch
        {
            "Articles" => AllRelatedItems.Where(i => i.Type == "Article").ToList(),
            "Videos" => AllRelatedItems.Where(i => i.Type == "Video").ToList(),
            _ => AllRelatedItems
        };

        DisplayedRelatedItems.Clear();
        foreach (var item in filtered)
        {
            DisplayedRelatedItems.Add(item);
        }
    }

    private async Task<List<RelatedStoryDisplayItem>> GetLocalFallbackRelatedStoriesAsync(string? title, string? category)
    {
        var list = new List<RelatedStoryDisplayItem>();
        try
        {
            var categories = await _storyRepository.GetCachedBriefingAsync(days: 14);
            var allArticles = categories.SelectMany(c => c.Top).ToList();

            var targetCategory = category?.Trim().ToLowerInvariant() ?? "";
            var matched = allArticles
                .Where(a => !string.Equals(a.Url, ArticleUrl, StringComparison.OrdinalIgnoreCase) &&
                            (string.IsNullOrEmpty(targetCategory) || a.Category?.ToLowerInvariant() == targetCategory))
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
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local fallback related stories error.");
        }
        return list;
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
}
