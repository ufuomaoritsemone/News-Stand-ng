using Microsoft.Extensions.Logging;
using NewsScraperService.Models;

namespace NewsScraperService.Services;

/// <summary>
/// Enriches article data by populating missing image URLs and normalizing titles.
/// Extracted from ScraperWorker to satisfy Single Responsibility Principle (Fix #15, #16).
/// </summary>
public interface IArticleEnricher
{
    /// <summary>
    /// Replaces missing or generic image URLs on articles using metadata scraping.
    /// </summary>
    Task PopulateMissingImageUrlsAsync(
        HttpClient client,
        IList<ArticleModel> items,
        string sourceName,
        CancellationToken ct = default);

    /// <summary>
    /// Normalizes an article title — removes pipe/dash suffixes and excess whitespace.
    /// </summary>
    string NormalizeTitle(string title);

    /// <summary>Returns true if the URL is a generic/placeholder news image.</summary>
    bool IsGenericImage(string? url);
}

/// <summary>
/// Default implementation of article enrichment logic.
/// </summary>
public class ArticleEnricher(
    ArticleContentExtractor extractor,
    ILogger<ArticleEnricher> logger) : IArticleEnricher
{
    private static readonly HashSet<string> GenericImageDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "placeholder.com", "via.placeholder.com", "picsum.photos",
        "lorempixel.com", "dummyimage.com"
    };

    private static readonly string[] GenericImagePatterns =
    [
        "default", "placeholder", "no-image", "noimage", "blank",
        "logo", "favicon", "icon-", "-icon", "avatar-default", "fallback", "header"
    ];

    public bool IsGenericImage(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return true;

        if (GenericImageDomains.Contains(uri.Host)) return true;

        var lowerPath = uri.PathAndQuery.ToLowerInvariant();
        return GenericImagePatterns.Any(p => lowerPath.Contains(p));
    }

    public string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(title, "\"|'|\\s+", " ").Trim();
    }

    public async Task PopulateMissingImageUrlsAsync(
        HttpClient client,
        IList<ArticleModel> items,
        string sourceName,
        CancellationToken ct = default)
    {
        var itemsWithoutImages = items
            .Where(it => IsGenericImage(it.ImageUrl) && !string.IsNullOrEmpty(it.Url))
            .ToList();

        if (itemsWithoutImages.Count == 0) return;

        logger.LogInformation(
            "Resolving missing images and rich metadata for {Count} articles from {Source}...",
            itemsWithoutImages.Count, sourceName);

        using var semaphore = new SemaphoreSlim(4);
        var tasks = itemsWithoutImages.Select(async it =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var extracted = await extractor.ExtractFromUrlAsync(client, it.Url!, sourceName, ct);
                if (extracted != null)
                {
                    if (!string.IsNullOrEmpty(extracted.ImageUrl) && !IsGenericImage(extracted.ImageUrl))
                    {
                        it.ImageUrl = extracted.ImageUrl;
                    }
                    if (string.IsNullOrEmpty(it.Content) && !string.IsNullOrEmpty(extracted.Content))
                    {
                        it.Content = extracted.Content;
                    }
                    if (string.IsNullOrEmpty(it.Summary) && !string.IsNullOrEmpty(extracted.Summary))
                    {
                        it.Summary = extracted.Summary;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Failed to resolve metadata for article '{Title}'", it.Title);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}
