using System.Security;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NewsApi.Controllers;
using NewsApi.Data;
using NewsApi.Models;
using Xunit;

namespace NewsApi.Tests;

public class NewsSitemapXmlTests
{
    private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly XNamespace NewsNs = "http://www.google.com/schemas/sitemap-news/0.9";

    private NewsDbContext CreateInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new NewsDbContext(options);
    }

    /// <summary>
    /// Helper to generate the exact XML string format expected for Google News sitemaps.
    /// Matches the production react-router web resource route output.
    /// </summary>
    public static string RenderNewsSitemapXml(IEnumerable<SitemapEntryDto> entries, string siteUrl = "https://newsstand.ng")
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(SitemapNs + "urlset",
                new XAttribute(XNamespace.Xmlns + "news", NewsNs.NamespaceName),
                entries.Select(e =>
                {
                    var canonicalLoc = e.Loc.StartsWith("http")
                        ? e.Loc
                        : $"{siteUrl.TrimEnd('/')}{(e.Loc.StartsWith('/') ? "" : "/")}{e.Loc}";

                    return new XElement(SitemapNs + "url",
                        new XElement(SitemapNs + "loc", canonicalLoc),
                        new XElement(NewsNs + "news",
                            new XElement(NewsNs + "publication",
                                new XElement(NewsNs + "name", e.PublicationName ?? "News Stand NG"),
                                new XElement(NewsNs + "language", e.PublicationLanguage ?? "en")
                            ),
                            new XElement(NewsNs + "publication_date", (e.PublishedAt ?? DateTime.UtcNow).ToString("yyyy-MM-ddTHH:mm:ssZ")),
                            new XElement(NewsNs + "title", e.Title ?? "News Story")
                        )
                    );
                })
            )
        );

        return doc.ToString();
    }

    [Fact]
    public async Task GoogleNewsSitemap_FiltersTo48Hours_ExcludesSponsoredAndOldArticles()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var now = DateTime.UtcNow;

        var freshArticle = new Article
        {
            Id = "art_fresh",
            Title = "Fresh Breaking News Within 48 Hours",
            Slug = "fresh-breaking-news-within-48-hours-111111",
            PublishedAt = now.AddHours(-24),
            IsSponsored = false
        };

        var justInWindowArticle = new Article
        {
            Id = "art_edge",
            Title = "Just Inside Cutoff News",
            Slug = "just-inside-cutoff-news-222222",
            PublishedAt = now.AddHours(-47),
            IsSponsored = false
        };

        var expiredArticle = new Article
        {
            Id = "art_expired",
            Title = "Expired Article Beyond 48 Hours",
            Slug = "expired-article-beyond-48-hours-333333",
            PublishedAt = now.AddHours(-49),
            IsSponsored = false
        };

        var sponsoredArticle = new Article
        {
            Id = "art_sponsored",
            Title = "Paid Promo Article Within 48 Hours",
            Slug = "paid-promo-article-within-48-hours-444444",
            PublishedAt = now.AddHours(-2),
            IsSponsored = true
        };

        db.Articles.AddRange(freshArticle, justInWindowArticle, expiredArticle, sponsoredArticle);
        await db.SaveChangesAsync();

        var controller = new SeoController(db, NullLogger<SeoController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var actionResult = await controller.GetSitemapEntries(type: "news");
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<SitemapEntriesResponse>(okResult.Value);

        // Verify only 2 articles meet the 48h non-sponsored criterion
        Assert.Equal(2, response.Entries.Count);
        Assert.Contains(response.Entries, e => e.Loc == $"/news/{freshArticle.Slug}");
        Assert.Contains(response.Entries, e => e.Loc == $"/news/{justInWindowArticle.Slug}");
        Assert.DoesNotContain(response.Entries, e => e.Loc == $"/news/{expiredArticle.Slug}");
        Assert.DoesNotContain(response.Entries, e => e.Loc == $"/news/{sponsoredArticle.Slug}");

        // Now render and validate the XML document
        var xml = RenderNewsSitemapXml(response.Entries);
        var parsedDoc = XDocument.Parse(xml);

        var urlElements = parsedDoc.Root!.Elements(SitemapNs + "url").ToList();
        Assert.Equal(2, urlElements.Count);

        foreach (var urlEl in urlElements)
        {
            var loc = urlEl.Element(SitemapNs + "loc")?.Value;
            var newsEl = urlEl.Element(NewsNs + "news");
            var pubEl = newsEl?.Element(NewsNs + "publication");
            var name = pubEl?.Element(NewsNs + "name")?.Value;
            var lang = pubEl?.Element(NewsNs + "language")?.Value;
            var date = newsEl?.Element(NewsNs + "publication_date")?.Value;
            var title = newsEl?.Element(NewsNs + "title")?.Value;

            Assert.NotNull(loc);
            Assert.StartsWith("https://newsstand.ng/news/", loc);
            Assert.Equal("News Stand NG", name);
            Assert.Equal("en", lang);
            Assert.NotNull(date);
            Assert.NotNull(title);
        }
    }

    [Fact]
    public void GoogleNewsSitemap_EscapesXmlSpecialCharacters_ProducesParsableXml()
    {
        // Special characters that would break XML if not escaped: &, <, >, ", '
        var specialTitle = "NNPCL & Dangote: Why \"Petrol\" Prices Dropped <50% in 'Lagos' & Abuja";
        var entries = new List<SitemapEntryDto>
        {
            new()
            {
                Loc = "/news/nnpcl-dangote-petrol-prices-123456",
                Title = specialTitle,
                PublicationName = "News Stand NG & Media",
                PublicationLanguage = "en",
                PublishedAt = DateTime.UtcNow.AddHours(-1)
            }
        };

        var xml = RenderNewsSitemapXml(entries);

        // Verify XML contains escaped entity &amp; and no raw < or & inside text
        Assert.Contains("&amp;", xml);

        // Verify XDocument successfully parses the XML string
        var doc = XDocument.Parse(xml);
        var titleElement = doc.Descendants(NewsNs + "title").FirstOrDefault();
        var pubNameElement = doc.Descendants(NewsNs + "name").FirstOrDefault();

        Assert.NotNull(titleElement);
        // Parsed XML element value should be the original decoded string
        Assert.Equal(specialTitle, titleElement.Value);

        Assert.NotNull(pubNameElement);
        Assert.Equal("News Stand NG & Media", pubNameElement.Value);
    }
}
