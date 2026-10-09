using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

/// <summary>
/// Centralized service handling user interactions on news stories and video stories:
/// opening articles in reader/web view, launching video stories, toggling bookmarks with
/// accessibility announcements, system sharing, and 𝕏 (Twitter) sharing (Issue #34 / Fix 4).
/// </summary>
public interface IArticleInteractionService
{
    /// <summary>
    /// Opens the specified article in the reader view, recording read history and tracking clicks if sponsored.
    /// </summary>
    Task OpenArticleAsync(BriefingItem? item);

    /// <summary>
    /// Opens the specified video story in the player view.
    /// </summary>
    Task OpenVideoAsync(VideoStoryItem? story);

    /// <summary>
    /// Toggles the bookmark status of an article and announces the state change to screen readers.
    /// </summary>
    bool ToggleBookmark(BriefingItem? item);

    /// <summary>
    /// Triggers the system share dialog for the given news article.
    /// </summary>
    Task ShareArticleAsync(BriefingItem? item);

    /// <summary>
    /// Shares the given news article to 𝕏 (Twitter) with deep link and card resolution.
    /// </summary>
    Task ShareArticleOnXAsync(BriefingItem? item);

    /// <summary>
    /// Shares the given news article directly on WhatsApp with rich preview link.
    /// </summary>
    Task ShareArticleOnWhatsAppAsync(BriefingItem? item);
}
