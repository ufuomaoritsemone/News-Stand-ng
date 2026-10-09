using System.Diagnostics;
using NigerianNewsGrid.Client.Helpers;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Constants;

namespace NigerianNewGrid.Services;

/// <summary>
/// Provides high-level dispatching for sharing news stories to 𝕏 (formerly Twitter).
/// Encapsulates URL intent resolution, native app deep linking, character budgeting,
/// publisher handle attribution, and store download CTA (Issue #20).
/// </summary>
public class XShareService : IXShareService
{
    public const string GooglePlayStoreUrl = AppStoreConstants.GooglePlayStoreUrl;
    public const string AppleAppStoreUrl   = AppStoreConstants.AppleAppStoreUrl;

    public static XShareService Instance { get; } = new();

    Task IXShareService.ShareStoryOnXAsync(BriefingItem item)
        => ShareStoryOnXAsync(item);

    Task IXShareService.ShareStoryOnXAsync(string? title, string? source, string? storyUrl, string? articleId, string? slug)
        => ShareStoryOnXAsync(title, source, storyUrl, articleId, slug);

    /// <summary>
    /// Shares a <see cref="BriefingItem"/> directly on 𝕏.
    /// </summary>
    public static async Task ShareStoryOnXAsync(BriefingItem item)
    {
        if (item == null) return;
        await ShareStoryOnXAsync(item.Title, item.Source, item.Url, item.Id, item.Slug);
    }

    /// <summary>
    /// Shares an article to 𝕏 with title, source attribution, story URL, and optional app download CTA.
    /// </summary>
    public static async Task ShareStoryOnXAsync(
        string? title,
        string? source,
        string? storyUrl,
        string? articleId = null,
        string? slug = null)
    {
        try
        {
            var resolvedTitle = string.IsNullOrWhiteSpace(title) ? "Breaking News Story" : title;
            var shareUrl = ResolveShareUrl(storyUrl, articleId, slug, resolvedTitle);
            var appDownloadUrl = !string.IsNullOrWhiteSpace(GooglePlayStoreUrl) ? GooglePlayStoreUrl : null;

            var tweetText = XShareFormatter.BuildTweetText(
                title: resolvedTitle,
                source: source,
                appDownloadUrl: appDownloadUrl,
                shareUrl: shareUrl);

            var nativeAppUri = XShareFormatter.BuildNativeIntentUri(tweetText, shareUrl);
            var webIntentUri = XShareFormatter.BuildWebIntentUri(tweetText, shareUrl);

            // 1. Try launching the native 𝕏 (Twitter) app directly
            try
            {
                if (await Launcher.Default.CanOpenAsync(nativeAppUri))
                {
                    var launched = await Launcher.Default.OpenAsync(nativeAppUri);
                    if (launched) return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[XShareService] Native 𝕏 app launch fallback: {ex.Message}");
            }

            // 2. Fallback to system browser 𝕏 Web Intent
            await Launcher.Default.OpenAsync(webIntentUri);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[XShareService] Share on 𝕏 error: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolves the optimal share URL for 𝕏 cards.
    /// Always resolves to the canonical newsstand.ng article URL so 𝕏 can unfurl rich card metadata.
    /// </summary>
    private static string ResolveShareUrl(string? storyUrl, string? articleId, string? slug = null, string? title = null)
    {
        return NewsStandUrlHelper.GetArticleWebUrl(slug, articleId, title, storyUrl);
    }
}
