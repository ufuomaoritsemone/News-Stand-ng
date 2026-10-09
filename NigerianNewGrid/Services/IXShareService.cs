using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

/// <summary>
/// Service abstraction for sharing stories and articles to 𝕏 (formerly Twitter).
/// </summary>
public interface IXShareService
{
    Task ShareStoryOnXAsync(BriefingItem item);
    Task ShareStoryOnXAsync(string? title, string? source, string? storyUrl, string? articleId = null, string? slug = null);
}
