using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using NewsScraperService.Models;
using SmartReader;

namespace NewsScraperService.Services;

/// <summary>
/// Multi-tiered intelligent article content and metadata extractor.
/// Combines Schema.org JSON-LD parsing, Mozilla Readability heuristics (SmartReader),
/// and OpenGraph/Twitter meta tag fallback.
/// </summary>
public class ArticleContentExtractor
{
    private readonly ILogger<ArticleContentExtractor> _logger;

    public ArticleContentExtractor(ILogger<ArticleContentExtractor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Fetches an article by URL and extracts full structured content, image, summary, and metadata.
    /// </summary>
    public async Task<ArticleModel?> ExtractFromUrlAsync(
        HttpClient client, 
        string url, 
        string sourceName, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10)); // 10s timeout per article page

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("HTTP {StatusCode} fetching article at {Url}", response.StatusCode, url);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cts.Token);
            if (string.IsNullOrWhiteSpace(html)) return null;

            return ExtractFromHtml(html, url, sourceName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Failed to extract article content from {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Extracts structured article data from raw HTML content.
    /// </summary>
    public ArticleModel ExtractFromHtml(string html, string url, string sourceName)
    {
        var model = new ArticleModel
        {
            Url = url,
            Source = sourceName
        };

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // --- 1. Tier 1: Schema.org JSON-LD Extraction ---
        var jsonLdSuccess = TryExtractJsonLd(doc, model);

        // --- 2. Tier 2: Mozilla Readability (SmartReader) Heuristics ---
        try
        {
            var reader = new Reader(url, html);
            var readerArticle = reader.GetArticle();
            if (readerArticle?.IsReadable == true)
            {
                if (string.IsNullOrWhiteSpace(model.Title) && !string.IsNullOrWhiteSpace(readerArticle.Title))
                {
                    model.Title = readerArticle.Title;
                }

                if (string.IsNullOrWhiteSpace(model.Summary) && !string.IsNullOrWhiteSpace(readerArticle.Excerpt))
                {
                    model.Summary = readerArticle.Excerpt;
                }

                if (string.IsNullOrWhiteSpace(model.Content) && !string.IsNullOrWhiteSpace(readerArticle.TextContent))
                {
                    model.Content = readerArticle.TextContent;
                }

                if (string.IsNullOrWhiteSpace(model.ImageUrl) && !string.IsNullOrWhiteSpace(readerArticle.FeaturedImage))
                {
                    model.ImageUrl = readerArticle.FeaturedImage;
                }

                if (!model.PublishedAt.HasValue && readerArticle.PublicationDate.HasValue)
                {
                    model.PublishedAt = readerArticle.PublicationDate.Value.ToUniversalTime();
                }

                if (string.IsNullOrWhiteSpace(model.Author) && !string.IsNullOrWhiteSpace(readerArticle.Byline))
                {
                    model.Author = readerArticle.Byline;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "SmartReader extraction bypassed for {Url}", url);
        }

        // --- 3. Tier 3: Meta Tag Fallbacks (OpenGraph, Twitter, Standard Meta) ---
        ExtractMetaFallback(doc, model);

        // --- 4. Final Sanitization and Fallbacks ---
        model.Title = SanitizeText(model.Title);
        model.Summary = SanitizeText(model.Summary ?? string.Empty);
        model.Content = SanitizeContentBody(model.Content);
        if (!string.IsNullOrWhiteSpace(model.Author))
        {
            var cleanAuthor = SanitizeText(model.Author);
            if (cleanAuthor.StartsWith("by ", StringComparison.OrdinalIgnoreCase))
                cleanAuthor = cleanAuthor[3..].Trim();
            model.Author = string.IsNullOrWhiteSpace(cleanAuthor) ? null : cleanAuthor;
        }

        if (string.IsNullOrWhiteSpace(model.Summary) && !string.IsNullOrWhiteSpace(model.Content))
        {
            // Generate summary from first 250 characters of content
            model.Summary = model.Content.Length > 250
                ? model.Content[..250].Trim() + "..."
                : model.Content;
        }

        if (!model.PublishedAt.HasValue)
        {
            model.PublishedAt = DateTime.UtcNow;
        }

        return model;
    }

    /// <summary>
    /// Parses JSON-LD script blocks looking for NewsArticle, Article, or WebPage schemas.
    /// </summary>
    public static bool TryExtractJsonLd(HtmlDocument doc, ArticleModel model)
    {
        var scripts = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
        if (scripts == null) return false;

        foreach (var script in scripts)
        {
            var rawJson = script.InnerText?.Trim();
            if (string.IsNullOrWhiteSpace(rawJson)) continue;

            try
            {
                using var jsonDoc = JsonDocument.Parse(rawJson);
                var root = jsonDoc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        if (ProcessJsonLdElement(item, model)) return true;
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in graph.EnumerateArray())
                        {
                            if (ProcessJsonLdElement(item, model)) return true;
                        }
                    }
                    else
                    {
                        if (ProcessJsonLdElement(root, model)) return true;
                    }
                }
            }
            catch
            {
                // Skip invalid JSON-LD blocks
            }
        }

        return false;
    }

    private static bool ProcessJsonLdElement(JsonElement el, ArticleModel model)
    {
        if (!el.TryGetProperty("@type", out var typeProp)) return false;

        var type = typeProp.GetString();
        if (string.IsNullOrEmpty(type)) return false;

        bool isArticleType = type.Contains("Article", StringComparison.OrdinalIgnoreCase) ||
                             type.Contains("NewsArticle", StringComparison.OrdinalIgnoreCase) ||
                             type.Contains("BlogPosting", StringComparison.OrdinalIgnoreCase) ||
                             type.Contains("Report", StringComparison.OrdinalIgnoreCase);

        if (!isArticleType) return false;

        // Headline
        if (el.TryGetProperty("headline", out var headlineProp))
        {
            model.Title = headlineProp.GetString() ?? string.Empty;
        }
        else if (el.TryGetProperty("name", out var nameProp))
        {
            model.Title = nameProp.GetString() ?? string.Empty;
        }

        // Description
        if (el.TryGetProperty("description", out var descProp))
        {
            model.Summary = descProp.GetString();
        }

        // Article Body
        if (el.TryGetProperty("articleBody", out var bodyProp))
        {
            model.Content = bodyProp.GetString();
        }

        // Publication Date
        if (el.TryGetProperty("datePublished", out var datePublishedProp))
        {
            if (DateTimeOffset.TryParse(datePublishedProp.GetString(), out var dto))
            {
                model.PublishedAt = dto.UtcDateTime;
            }
        }
        else if (el.TryGetProperty("dateModified", out var dateModProp))
        {
            if (DateTimeOffset.TryParse(dateModProp.GetString(), out var dto))
            {
                model.PublishedAt = dto.UtcDateTime;
            }
        }

        // Image
        if (el.TryGetProperty("image", out var imageProp))
        {
            if (imageProp.ValueKind == JsonValueKind.String)
            {
                model.ImageUrl = imageProp.GetString();
            }
            else if (imageProp.ValueKind == JsonValueKind.Object && imageProp.TryGetProperty("url", out var imgUrlProp))
            {
                model.ImageUrl = imgUrlProp.GetString();
            }
            else if (imageProp.ValueKind == JsonValueKind.Array && imageProp.GetArrayLength() > 0)
            {
                var first = imageProp[0];
                if (first.ValueKind == JsonValueKind.String)
                {
                    model.ImageUrl = first.GetString();
                }
                else if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("url", out var arrayImgUrl))
                {
                    model.ImageUrl = arrayImgUrl.GetString();
                }
            }
        }

        // Author
        if (string.IsNullOrWhiteSpace(model.Author) && el.TryGetProperty("author", out var authorProp))
        {
            if (authorProp.ValueKind == JsonValueKind.String)
            {
                model.Author = authorProp.GetString();
            }
            else if (authorProp.ValueKind == JsonValueKind.Object && authorProp.TryGetProperty("name", out var authorNameProp))
            {
                model.Author = authorNameProp.GetString();
            }
            else if (authorProp.ValueKind == JsonValueKind.Array && authorProp.GetArrayLength() > 0)
            {
                var firstAuthor = authorProp[0];
                if (firstAuthor.ValueKind == JsonValueKind.String)
                {
                    model.Author = firstAuthor.GetString();
                }
                else if (firstAuthor.ValueKind == JsonValueKind.Object && firstAuthor.TryGetProperty("name", out var nameProp))
                {
                    model.Author = nameProp.GetString();
                }
            }
        }

        // Category / Section
        if (el.TryGetProperty("articleSection", out var sectionProp))
        {
            if (sectionProp.ValueKind == JsonValueKind.String)
            {
                model.Category = sectionProp.GetString();
            }
        }

        return !string.IsNullOrWhiteSpace(model.Title);
    }

    private static void ExtractMetaFallback(HtmlDocument doc, ArticleModel model)
    {
        // Author fallback
        if (string.IsNullOrWhiteSpace(model.Author))
        {
            var author = GetMetaContent(doc, "author")
                ?? GetMetaContent(doc, "article:author")
                ?? GetMetaContent(doc, "twitter:creator")
                ?? GetMetaContent(doc, "sailthru.author")
                ?? GetMetaContent(doc, "byl");
            if (!string.IsNullOrWhiteSpace(author)) model.Author = author;
        }

        // OpenGraph Title fallback
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            var ogTitle = GetMetaContent(doc, "og:title") ?? GetMetaContent(doc, "twitter:title");
            if (!string.IsNullOrWhiteSpace(ogTitle)) model.Title = ogTitle;
        }

        // HTML <title> fallback
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            var titleNode = doc.DocumentNode.SelectSingleNode("//title");
            if (titleNode != null)
            {
                model.Title = titleNode.InnerText;
            }
        }

        // OpenGraph Description fallback
        if (string.IsNullOrWhiteSpace(model.Summary))
        {
            var ogDesc = GetMetaContent(doc, "og:description") 
                ?? GetMetaContent(doc, "twitter:description") 
                ?? GetMetaContent(doc, "description");
            if (!string.IsNullOrWhiteSpace(ogDesc)) model.Summary = ogDesc;
        }

        // OpenGraph Image fallback
        if (string.IsNullOrWhiteSpace(model.ImageUrl) || IsGenericImage(model.ImageUrl))
        {
            var ogImg = GetMetaContent(doc, "og:image") 
                ?? GetMetaContent(doc, "twitter:image") 
                ?? GetMetaContent(doc, "twitter:image:src");
            if (!string.IsNullOrWhiteSpace(ogImg) && !IsGenericImage(ogImg))
            {
                model.ImageUrl = ogImg;
            }
        }

        // Publication Time fallback
        if (!model.PublishedAt.HasValue)
        {
            var pubTime = GetMetaContent(doc, "article:published_time") 
                ?? GetMetaContent(doc, "og:article:published_time")
                ?? GetMetaContent(doc, "publication_date")
                ?? GetMetaContent(doc, "date");
            if (!string.IsNullOrWhiteSpace(pubTime) && DateTimeOffset.TryParse(pubTime, out var dto))
            {
                model.PublishedAt = dto.UtcDateTime;
            }
        }
    }

    private static string? GetMetaContent(HtmlDocument doc, string propertyOrName)
    {
        var node = doc.DocumentNode.SelectSingleNode($"//meta[@property='{propertyOrName}']")
            ?? doc.DocumentNode.SelectSingleNode($"//meta[@name='{propertyOrName}']");
        var content = node?.GetAttributeValue("content", string.Empty)?.Trim();
        return string.IsNullOrWhiteSpace(content) ? null : content;
    }

    public static string SanitizeText(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
        var clean = Regex.Replace(rawText, "<.*?>", string.Empty);
        clean = Regex.Replace(clean, @"\s+", " ");
        return System.Net.WebUtility.HtmlDecode(clean).Trim();
    }

    public static string? SanitizeContentBody(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var clean = Regex.Replace(content, "<.*?>", string.Empty);
        clean = System.Net.WebUtility.HtmlDecode(clean);

        // Remove common news boilerplate lines
        var lines = clean.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var cleanLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            if (trimmed.StartsWith("READ ALSO:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Read Also:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Also Read:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("All rights reserved. This material, and other digital content", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Join our WhatsApp Channel", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Follow us on Twitter", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Click here to download", StringComparison.OrdinalIgnoreCase) ||
                trimmed.Contains("Share this story", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            cleanLines.Add(trimmed);
        }

        return string.Join("\n\n", cleanLines).Trim();
    }

    private static bool IsGenericImage(string? url)
    {
        if (string.IsNullOrEmpty(url)) return true;
        return url.Contains("logo", StringComparison.OrdinalIgnoreCase) 
            || url.Contains("default", StringComparison.OrdinalIgnoreCase)
            || url.Contains("fallback", StringComparison.OrdinalIgnoreCase)
            || url.Contains("header", StringComparison.OrdinalIgnoreCase)
            || url.Contains("icon", StringComparison.OrdinalIgnoreCase)
            || url.Contains("avatar", StringComparison.OrdinalIgnoreCase);
    }
}
