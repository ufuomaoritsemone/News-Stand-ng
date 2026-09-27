using System.Xml.Linq;
using NewsScraperService.Models;

namespace NewsScraperService.Scrapers;

public abstract class RssScraperBase : IScraper
{
    protected readonly HttpClient _http;

    protected RssScraperBase(HttpClient http)
    {
        _http = http;
    }

    public abstract string SourceId { get; }
    public abstract string SourceName { get; }
    public abstract string RssUrl { get; }

    public virtual string? DefaultCategory => null;

    public async Task<IEnumerable<ArticleModel>> ScrapeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, RssUrl);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            using var resp = await _http.SendAsync(request, cancellationToken);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
            var doc = XDocument.Load(stream);
            var list = new List<ArticleModel>();

            // 1. Standard RSS 2.0 <item> elements
            var items = doc.Descendants().Where(e => e.Name.LocalName == "item");
            foreach (var item in items)
            {
                var title = item.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value ?? string.Empty;
                var link = item.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Value;
                var pub = item.Elements().FirstOrDefault(e => e.Name.LocalName == "pubDate")?.Value;
                DateTime? published = null;
                if (DateTimeOffset.TryParse(pub, out var dto)) published = dto.UtcDateTime;
                var description = item.Elements().FirstOrDefault(e => e.Name.LocalName == "description")?.Value;
                var content = item.Elements().FirstOrDefault(e => e.Name.LocalName == "encoded")?.Value;

                var imageUrl = ExtractImageUrl(item, description, content);

                list.Add(new ArticleModel
                {
                    Title = title,
                    Url = link,
                    Summary = description ?? content,
                    PublishedAt = published,
                    Source = SourceName,
                    ImageUrl = imageUrl,
                    Category = DefaultCategory
                });
            }

            // 2. Atom <entry> elements (e.g. Linda Ikeji, Blogger/Atom syndication)
            if (list.Count == 0)
            {
                var entries = doc.Descendants().Where(e => e.Name.LocalName == "entry");
                foreach (var entry in entries)
                {
                    var title = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value ?? string.Empty;
                    var link = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "link" && (e.Attribute("rel") == null || e.Attribute("rel")?.Value == "alternate"))?.Attribute("href")?.Value
                               ?? entry.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Attribute("href")?.Value;
                    var pub = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "published")?.Value 
                              ?? entry.Elements().FirstOrDefault(e => e.Name.LocalName == "updated")?.Value;
                    DateTime? published = null;
                    if (DateTimeOffset.TryParse(pub, out var dto)) published = dto.UtcDateTime;
                    var summary = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "summary")?.Value 
                                  ?? entry.Elements().FirstOrDefault(e => e.Name.LocalName == "content")?.Value;

                    var imageUrl = ExtractImageUrl(entry, summary, null);

                    list.Add(new ArticleModel
                    {
                        Title = title,
                        Url = link,
                        Summary = summary,
                        PublishedAt = published,
                        Source = SourceName,
                        ImageUrl = imageUrl,
                        Category = DefaultCategory
                    });
                }
            }

            return list;
        }
        catch
        {
            return Array.Empty<ArticleModel>();
        }
    }

    protected static string? ExtractImageUrl(XElement element, string? description, string? content)
    {
        // 1. Yahoo Media RSS: <media:content url="..." /> or <media:thumbnail url="..." />
        XNamespace media = "http://search.yahoo.com/mrss/";
        var mediaContent = element.Elements().FirstOrDefault(e => e.Name.LocalName == "content" && (e.Name.Namespace == media || string.IsNullOrEmpty(e.Name.NamespaceName)));
        var mediaThumbnail = element.Elements().FirstOrDefault(e => e.Name.LocalName == "thumbnail" && (e.Name.Namespace == media || string.IsNullOrEmpty(e.Name.NamespaceName)));
        var mediaUrl = mediaContent?.Attribute("url")?.Value ?? mediaThumbnail?.Attribute("url")?.Value;
        if (!string.IsNullOrWhiteSpace(mediaUrl) && mediaUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return mediaUrl;

        // 2. Enclosure: <enclosure url="..." type="image/..." />
        var enclosure = element.Elements().FirstOrDefault(e => e.Name.LocalName == "enclosure" && (e.Attribute("type")?.Value?.Contains("image", StringComparison.OrdinalIgnoreCase) == true || string.IsNullOrEmpty(e.Attribute("type")?.Value)));
        var encUrl = enclosure?.Attribute("url")?.Value;
        if (!string.IsNullOrWhiteSpace(encUrl) && encUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return encUrl;

        // 3. Atom image link: <link rel="enclosure" type="image/..." href="..." />
        var atomImageLink = element.Elements().FirstOrDefault(e => e.Name.LocalName == "link" && e.Attribute("type")?.Value?.Contains("image", StringComparison.OrdinalIgnoreCase) == true)?.Attribute("href")?.Value;
        if (!string.IsNullOrWhiteSpace(atomImageLink) && atomImageLink.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return atomImageLink;

        // 4. Regex extraction from HTML description/content (e.g. Linda Ikeji embeds <img src="..."> in summary)
        var combinedText = (description ?? string.Empty) + " " + (content ?? string.Empty);
        var match = System.Text.RegularExpressions.Regex.Match(combinedText, @"<img\s+[^>]*src=[""'](https?://[^""']+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
            return match.Groups[1].Value;

        return null;
    }
}
