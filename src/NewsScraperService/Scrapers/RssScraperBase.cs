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
