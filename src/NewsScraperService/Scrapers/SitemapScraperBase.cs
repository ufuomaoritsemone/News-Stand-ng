using System.Xml.Linq;
using NewsScraperService.Models;
using NewsScraperService.Services;

namespace NewsScraperService.Scrapers;

/// <summary>
/// Abstract base class for scrapers that discover articles via Google News Sitemaps and standard XML sitemaps,
/// enriching them with full content, structured schema, and hero images.
/// </summary>
public abstract class SitemapScraperBase : IScraper
{
    protected readonly HttpClient _http;
    protected readonly ArticleContentExtractor? _extractor;
    protected readonly UrlFrontierManager? _frontier;

    protected SitemapScraperBase(
        HttpClient http, 
        ArticleContentExtractor? extractor = null, 
        UrlFrontierManager? frontier = null)
    {
        _http = http;
        _extractor = extractor;
        _frontier = frontier;
    }

    public abstract string SourceId { get; }
    public abstract string SourceName { get; }
    public abstract string SitemapUrl { get; }
    public virtual string? FallbackRssUrl => null;

    /// <summary>
    /// Maximum number of discovered URLs to enrich per scraping cycle to maintain high responsiveness.
    /// </summary>
    protected virtual int MaxArticlesPerCycle => 25;

    public virtual async Task<IEnumerable<ArticleModel>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        var discoveredItems = new List<ArticleModel>();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, SitemapUrl);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

            using var resp = await _http.SendAsync(request, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
                var doc = XDocument.Load(stream);
                discoveredItems = ParseSitemapDocument(doc);
            }
        }
        catch
        {
            // If sitemap parsing encounters an error, proceed to fallback
        }

        // Fallback to RSS if sitemap yielded no items and fallback is available
        if (discoveredItems.Count == 0 && !string.IsNullOrWhiteSpace(FallbackRssUrl))
        {
            discoveredItems = (await ScrapeFallbackRssAsync(FallbackRssUrl, cancellationToken)).ToList();
        }

        if (discoveredItems.Count == 0) return Array.Empty<ArticleModel>();

        // Filter unseen URLs via Frontier if available
        var candidatesToEnrich = discoveredItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Url) && (_frontier == null || !_frontier.IsUrlSeen(item.Url)))
            .Take(MaxArticlesPerCycle)
            .ToList();

        // If no extractor is injected, return the discovered sitemap items directly
        if (_extractor == null || candidatesToEnrich.Count == 0)
        {
            return discoveredItems.Take(MaxArticlesPerCycle);
        }

        // Enrich articles in parallel with politeness throttling (max 3 concurrent per domain)
        using var semaphore = new SemaphoreSlim(3);
        var enrichedArticles = new List<ArticleModel>();

        var tasks = candidatesToEnrich.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var enriched = await _extractor.ExtractFromUrlAsync(_http, item.Url!, SourceName, cancellationToken);
                if (enriched != null)
                {
                    // Preserve any title/date already known from the sitemap if the extracted one is empty
                    if (string.IsNullOrWhiteSpace(enriched.Title) && !string.IsNullOrWhiteSpace(item.Title))
                    {
                        enriched.Title = item.Title;
                    }
                    if (!enriched.PublishedAt.HasValue && item.PublishedAt.HasValue)
                    {
                        enriched.PublishedAt = item.PublishedAt;
                    }
                    enriched.Source = SourceName;

                    lock (enrichedArticles)
                    {
                        enrichedArticles.Add(enriched);
                    }

                    _frontier?.MarkUrlSeen(item.Url);
                    _frontier?.MarkTitleSeen(enriched.Title);
                }
                else
                {
                    // Fallback to basic sitemap item if page extract failed
                    item.Source = SourceName;
                    lock (enrichedArticles)
                    {
                        enrichedArticles.Add(item);
                    }
                    _frontier?.MarkUrlSeen(item.Url);
                }
            }
            catch
            {
                lock (enrichedArticles)
                {
                    enrichedArticles.Add(item);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return enrichedArticles;
    }

    public List<ArticleModel> ParseSitemapDocument(XDocument doc)
    {
        var list = new List<ArticleModel>();
        XNamespace sm = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace news = "http://www.google.com/schemas/sitemap-news/0.9";
        XNamespace image = "http://www.google.com/schemas/sitemap-image/1.1";

        // Query all <url> nodes regardless of default namespace prefix
        var urlNodes = doc.Descendants().Where(e => e.Name.LocalName == "url");

        foreach (var urlNode in urlNodes)
        {
            var loc = urlNode.Elements().FirstOrDefault(e => e.Name.LocalName == "loc")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(loc)) continue;

            string? title = null;
            DateTime? publishedAt = null;
            string? imageUrl = null;

            // Check Google News schema: <news:news>
            var newsNode = urlNode.Elements().FirstOrDefault(e => e.Name.LocalName == "news");
            if (newsNode != null)
            {
                title = newsNode.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value?.Trim();
                var pubDate = newsNode.Elements().FirstOrDefault(e => e.Name.LocalName == "publication_date")?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(pubDate) && DateTimeOffset.TryParse(pubDate, out var dto))
                {
                    publishedAt = dto.UtcDateTime;
                }
            }

            // Check <lastmod> if no news:publication_date was found
            if (!publishedAt.HasValue)
            {
                var lastMod = urlNode.Elements().FirstOrDefault(e => e.Name.LocalName == "lastmod")?.Value?.Trim();
                if (!string.IsNullOrWhiteSpace(lastMod) && DateTimeOffset.TryParse(lastMod, out var dto))
                {
                    publishedAt = dto.UtcDateTime;
                }
            }

            // Check image sitemap: <image:image><image:loc>
            var imgNode = urlNode.Elements().FirstOrDefault(e => e.Name.LocalName == "image");
            if (imgNode != null)
            {
                imageUrl = imgNode.Elements().FirstOrDefault(e => e.Name.LocalName == "loc")?.Value?.Trim();
            }

            list.Add(new ArticleModel
            {
                Url = loc,
                Title = title ?? string.Empty,
                PublishedAt = publishedAt ?? DateTime.UtcNow,
                ImageUrl = imageUrl,
                Source = SourceName
            });
        }

        return list;
    }

    protected async Task<IEnumerable<ArticleModel>> ScrapeFallbackRssAsync(string rssUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, rssUrl);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

            using var resp = await _http.SendAsync(request, cancellationToken);
            if (!resp.IsSuccessStatusCode) return Array.Empty<ArticleModel>();

            using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
            var doc = XDocument.Load(stream);
            var items = doc.Descendants("item");
            var list = new List<ArticleModel>();

            foreach (var item in items)
            {
                var title = item.Element("title")?.Value ?? string.Empty;
                var link = item.Element("link")?.Value;
                var pub = item.Element("pubDate")?.Value;
                DateTime? published = null;
                if (DateTimeOffset.TryParse(pub, out var dto)) published = dto.UtcDateTime;
                var description = item.Element("description")?.Value;

                XNamespace media = "http://search.yahoo.com/mrss/";
                var mediaContent = item.Elements().FirstOrDefault(e => e.Name.LocalName == "content" && (e.Name.Namespace == media || string.IsNullOrEmpty(e.Name.NamespaceName)));
                var mediaThumbnail = item.Elements().FirstOrDefault(e => e.Name.LocalName == "thumbnail" && (e.Name.Namespace == media || string.IsNullOrEmpty(e.Name.NamespaceName)));
                var enclosure = item.Elements().FirstOrDefault(e => e.Name.LocalName == "enclosure" && e.Attribute("type")?.Value?.Contains("image", StringComparison.OrdinalIgnoreCase) == true);

                var imageUrl = mediaContent?.Attribute("url")?.Value
                    ?? mediaThumbnail?.Attribute("url")?.Value
                    ?? enclosure?.Attribute("url")?.Value;

                list.Add(new ArticleModel
                {
                    Title = title,
                    Url = link,
                    Summary = description,
                    PublishedAt = published,
                    Source = SourceName,
                    ImageUrl = imageUrl
                });
            }

            return list;
        }
        catch
        {
            return Array.Empty<ArticleModel>();
        }
    }
}
