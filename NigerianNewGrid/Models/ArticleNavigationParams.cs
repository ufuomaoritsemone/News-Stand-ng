using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Models;

/// <summary>
/// Strongly-typed navigation parameter object for <see cref="ArticleWebPage"/> and <see cref="ViewModels.ArticleViewModel"/>.
/// Replaces sprawling multi-parameter constructors with a clean, cohesive record.
/// </summary>
public record ArticleNavigationParams(
    string Url,
    string? Title = null,
    string? ImageUrl = null,
    string? Category = null,
    string? ArticleId = null,
    bool IsSponsored = false,
    string? SponsorName = null,
    string? Content = null,
    string? Summary = null,
    string? Author = null,
    string? ContentType = null,
    bool HasAiSummary = false,
    string? AiSummary = null,
    string? AiSummaryEngine = null,
    IReadOnlyList<string>? KeyTakeaways = null,
    string? Slug = null)
{
    public static ArticleNavigationParams FromBriefingItem(BriefingItem item, bool isSponsored = false, string? sponsorName = null) =>
        new(
            Url: item.Url ?? string.Empty,
            Title: item.Title,
            ImageUrl: item.ImageUrl,
            Category: item.Category,
            ArticleId: item.Id,
            IsSponsored: isSponsored || item.IsSponsored,
            SponsorName: sponsorName ?? item.SponsorName,
            Content: item.Content,
            Summary: item.Summary,
            Author: item.Author,
            ContentType: item.ContentType,
            HasAiSummary: item.HasAiSummary,
            AiSummary: item.AiSummary,
            AiSummaryEngine: item.AiSummaryEngine,
            KeyTakeaways: item.KeyTakeaways,
            Slug: item.Slug);
}
