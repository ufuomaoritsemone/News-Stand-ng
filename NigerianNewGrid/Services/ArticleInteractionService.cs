using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Helpers;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Extensions;

namespace NigerianNewGrid.Services;

/// <summary>
/// Default implementation of <see cref="IArticleInteractionService"/> (Fix 4 / Issue #34).
/// Centralizes navigation, read recording, sponsored click tracking, bookmarking, and multi-channel sharing.
/// </summary>
public class ArticleInteractionService : IArticleInteractionService
{
    private const string AppShareTagline =
        "\n\n📲 Follow news as it breaks — download News Stand NG and never miss a story.";

    private readonly NewsApiClient _apiClient;
    private readonly IRecentlyReadService _recentlyReadService;
    private readonly IBookmarkService _bookmarkService;
    private readonly IXShareService _xShareService;
    private readonly ILogger<ArticleInteractionService> _logger;

    public ArticleInteractionService(
        NewsApiClient apiClient,
        IRecentlyReadService recentlyReadService,
        IBookmarkService bookmarkService,
        IXShareService xShareService,
        ILogger<ArticleInteractionService> logger)
    {
        _apiClient           = apiClient;
        _recentlyReadService = recentlyReadService;
        _bookmarkService     = bookmarkService;
        _xShareService       = xShareService;
        _logger              = logger;
    }

    public async Task OpenArticleAsync(BriefingItem? item)
    {
        if (item == null || item.IsAdMobPlaceholder) return;

        try
        {
            _recentlyReadService.RecordRead(item);

            if (item.IsSponsored)
            {
                if (!string.IsNullOrWhiteSpace(item.Id))
                {
                    _apiClient.TrackArticleClickAsync(item.Id).SafeFireAndForget(_logger, "TrackArticleClick");
                }

                var targetUrl = !string.IsNullOrWhiteSpace(item.SponsorUrl) ? item.SponsorUrl.Trim() : item.Url?.Trim();
                if (string.IsNullOrWhiteSpace(targetUrl) || !Uri.TryCreate(targetUrl, UriKind.Absolute, out _)) return;

                var sponsoredPage = new ArticleWebPage(
                    targetUrl,
                    item.Title,
                    item.ImageUrl,
                    item.Category,
                    item.Id,
                    isSponsored: true,
                    sponsorName: item.SponsorName,
                    content: item.Content,
                    summary: item.Summary);

                await PushPageAsync(sponsoredPage);
                return;
            }

            if (string.IsNullOrWhiteSpace(item.Url) || !Uri.TryCreate(item.Url, UriKind.Absolute, out _)) return;

            var articlePage = new ArticleWebPage(item);
            await PushPageAsync(articlePage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to article {ArticleId}", item.Id);
            Debug.WriteLine($"[ArticleInteractionService] Article navigation error: {ex.Message}");
        }
    }

    public async Task OpenVideoAsync(VideoStoryItem? story)
    {
        if (story == null) return;

        var targetUrl = !string.IsNullOrWhiteSpace(story.VideoUrl)
            ? story.VideoUrl
            : $"https://www.youtube.com/watch?v={story.VideoId}";

        if (string.IsNullOrWhiteSpace(targetUrl)) return;

        try
        {
            var videoPage = new ArticleWebPage(
                targetUrl,
                story.Title,
                story.DisplayThumbnailUrl,
                story.Category ?? "Video",
                story.Id);

            await PushPageAsync(videoPage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to video story {StoryId}", story.Id);
            Debug.WriteLine($"[ArticleInteractionService] Video navigation error: {ex.Message}");
        }
    }

    public bool ToggleBookmark(BriefingItem? item)
    {
        if (item == null) return false;

        bool added = _bookmarkService.ToggleBookmark(item);
        string msg = added ? $"Saved: {item.Title}" : "Bookmark removed.";
        SemanticScreenReader.Announce(msg);
        return added;
    }

    public async Task ShareArticleAsync(BriefingItem? item)
    {
        if (item == null) return;

        try
        {
            var newsstandUrl = NewsStandUrlHelper.GetArticleWebUrl(item);
            var shareText = $"Check out this story from {item.Source ?? "News Stand NG"}:\n\n{item.Title}\n\n{item.Summary}\n\nRead more: {newsstandUrl}{AppShareTagline}".Trim();
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = !string.IsNullOrWhiteSpace(item.Title) ? item.Title : "Share News Story",
                Text  = shareText,
                Uri   = newsstandUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger system share for article {ArticleId}", item.Id);
            Debug.WriteLine($"[ArticleInteractionService] Share error: {ex.Message}");
        }
    }

    public async Task ShareArticleOnWhatsAppAsync(BriefingItem? item)
    {
        if (item == null) return;

        try
        {
            var newsstandUrl = NewsStandUrlHelper.GetArticleWebUrl(item);
            var text = $"*{item.Title}*\n\n{item.Summary ?? string.Empty}\n\nRead more on News Stand NG: {newsstandUrl}";
            var encoded = Uri.EscapeDataString(text);
            try
            {
                await Launcher.Default.OpenAsync(new Uri($"https://wa.me/?text={encoded}"));
            }
            catch
            {
                await Share.Default.RequestAsync(new ShareTextRequest
                {
                    Title = item.Title,
                    Text  = text,
                    Uri   = newsstandUrl
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to share article {ArticleId} on WhatsApp", item.Id);
            Debug.WriteLine($"[ArticleInteractionService] WhatsApp share error: {ex.Message}");
        }
    }

    public async Task ShareArticleOnXAsync(BriefingItem? item)
    {
        if (item == null) return;

        try
        {
            await _xShareService.ShareStoryOnXAsync(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to share article {ArticleId} on X", item.Id);
            Debug.WriteLine($"[ArticleInteractionService] Share on X error: {ex.Message}");
        }
    }

    private static async Task PushPageAsync(Page page)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var nav = Shell.Current?.Navigation
                ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;

            if (nav != null)
            {
                await nav.PushAsync(page);
            }
        });
    }
}
