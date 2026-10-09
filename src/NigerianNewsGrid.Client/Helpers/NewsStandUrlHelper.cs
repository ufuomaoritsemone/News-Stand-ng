using System.Text.RegularExpressions;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewsGrid.Client.Helpers;

/// <summary>
/// Resolves the canonical public web URLs on https://newsstand.ng for news stories,
/// ensuring all mobile shares (system share sheet, WhatsApp, 𝕏/Twitter) link to the
/// official News Stand NG article landing page rather than third-party external origins.
/// </summary>
public static partial class NewsStandUrlHelper
{
    public const string ProductionSiteUrl = "https://newsstand.ng";

    /// <summary>
    /// Resolves the canonical public URL for an article on newsstand.ng.
    /// Prefers the canonical SEO slug, falling back to article ID, title-derived slug,
    /// or original fallback URL.
    /// </summary>
    public static string GetArticleWebUrl(
        string? slug,
        string? id = null,
        string? title = null,
        string? fallbackUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(slug))
        {
            var cleanSlug = slug.Trim().Trim('/');
            return $"{ProductionSiteUrl}/news/{cleanSlug}";
        }

        if (!string.IsNullOrWhiteSpace(id))
        {
            var cleanId = id.Trim().Trim('/');
            return $"{ProductionSiteUrl}/news/{Uri.EscapeDataString(cleanId)}";
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            var generatedSlug = Slugify(title);
            if (!string.IsNullOrWhiteSpace(generatedSlug))
            {
                return $"{ProductionSiteUrl}/news/{generatedSlug}";
            }
        }

        return !string.IsNullOrWhiteSpace(fallbackUrl) ? fallbackUrl.Trim() : ProductionSiteUrl;
    }

    /// <summary>
    /// Overload that resolves the canonical web URL for a <see cref="BriefingItem"/>.
    /// </summary>
    public static string GetArticleWebUrl(BriefingItem? item)
    {
        if (item == null) return ProductionSiteUrl;
        return GetArticleWebUrl(item.Slug, item.Id, item.Title, item.Url);
    }

    /// <summary>
    /// Generates a URL-safe lowercase slug from arbitrary title text.
    /// </summary>
    public static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var normalized = text.Trim().ToLowerInvariant();
        normalized = NonAlphanumericRegex().Replace(normalized, "-");
        normalized = MultipleHyphensRegex().Replace(normalized, "-");
        return normalized.Trim('-');
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleHyphensRegex();
}
