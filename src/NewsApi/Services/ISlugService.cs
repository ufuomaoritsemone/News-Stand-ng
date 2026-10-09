namespace NewsApi.Services;

/// <summary>
/// Service for generating canonical URL slugs and excerpt teasers for articles, sources, and topics.
/// </summary>
public interface ISlugService
{
    /// <summary>
    /// Generates a clean, normalized kebab-case slug from input text.
    /// Strips accents/diacritics, converts to lowercase ASCII, removes non-alphanumeric characters,
    /// and collapses consecutive hyphens.
    /// </summary>
    string GenerateSlug(string text, int maxLength = 80);

    /// <summary>
    /// Generates a collision-resistant article slug by suffixing a clean title slug with
    /// a 6-character base36 hash.
    /// </summary>
    string GenerateArticleSlug(string title, string? id = null, int maxLength = 80);

    /// <summary>
    /// Generates a clean teaser excerpt (up to maxChars, default 300) from summary or content body,
    /// stripping HTML tags, normalizing whitespace, and breaking cleanly at sentence or word boundaries.
    /// </summary>
    string GenerateExcerpt(string? content, string? summary, int maxChars = 300);
}
