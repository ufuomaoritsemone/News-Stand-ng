using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NewsScraperService.Scrapers;
using Xunit;

namespace NewsApiClient.Tests;

public class NewSourcesTests
{
    private class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task LindaIkejiScraper_ParsesAtomFeed_WithImagesAndEntertainmentCategory()
    {
        // Sample Atom feed matching Linda Ikeji's Blog syndication output
        var atomXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom">
            <title type="text">Linda Ikeji's Blog</title>
            <entry>
                <title type="text"><![CDATA[Burna Boy sells out 80,000 capacity stadium in London for historic concert]]></title>
                <link rel="alternate" type="text/html" href="https://www.lindaikejisblog.com/2026/09/burna-boy-historic-concert.html" />
                <id>tag:lindaikejisblog.com,2026:post-1001</id>
                <published>2026-09-03T14:30:00Z</published>
                <summary type="html"><![CDATA[<img src="https://www.lindaikejisblog.com/photos/shares/burna_concert.jfif">Grammy-award winning musician Burna Boy has achieved another groundbreaking milestone.]]></summary>
                <content type="html"><![CDATA[<img src="https://www.lindaikejisblog.com/photos/shares/burna_concert.jfif">Grammy-award winning musician Burna Boy has achieved another groundbreaking milestone.]]></content>
            </entry>
            <entry>
                <title type="text"><![CDATA[Nollywood box office hit breaks new weekend streaming record]]></title>
                <link rel="alternate" type="text/html" href="https://www.lindaikejisblog.com/2026/09/nollywood-box-office-record.html" />
                <id>tag:lindaikejisblog.com,2026:post-1002</id>
                <published>2026-09-03T12:00:00Z</published>
                <summary type="html"><![CDATA[The blockbuster movie directed by celebrated filmmakers continues to dominate.]]></summary>
            </entry>
        </feed>
        """;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(atomXml, System.Text.Encoding.UTF8, "application/atom+xml")
        };
        using var client = new HttpClient(new FakeHttpMessageHandler(response));
        var scraper = new LindaIkejiScraper(client);

        var articles = (await scraper.ScrapeAsync()).ToList();

        Assert.Equal(2, articles.Count);
        Assert.Equal("Linda Ikeji's Blog", scraper.SourceName);
        Assert.Equal("lindaikeji", scraper.SourceId);
        Assert.Equal("Entertainment", scraper.DefaultCategory);

        var first = articles[0];
        Assert.Equal("Burna Boy sells out 80,000 capacity stadium in London for historic concert", first.Title);
        Assert.Equal("https://www.lindaikejisblog.com/2026/09/burna-boy-historic-concert.html", first.Url);
        Assert.Equal("https://www.lindaikejisblog.com/photos/shares/burna_concert.jfif", first.ImageUrl);
        Assert.Equal("Entertainment", first.Category);
        Assert.Equal("Linda Ikeji's Blog", first.Source);
    }

    [Fact]
    public async Task BusinessDayScraper_ParsesRssFeed_WithBusinessCategory()
    {
        var rssXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0">
            <channel>
                <title>Businessday NG</title>
                <item>
                    <title><![CDATA[CBN maintains benchmark monetary policy rate to curb inflation]]></title>
                    <link>https://businessday.ng/news/article/cbn-maintains-rate/</link>
                    <pubDate>Thu, 03 Sep 2026 13:00:00 +0000</pubDate>
                    <description><![CDATA[The Monetary Policy Committee concluded its bi-monthly meeting today.]]></description>
                    <enclosure url="https://businessday.ng/wp-content/uploads/2026/09/cbn_building.jpg" type="image/jpeg" />
                </item>
            </channel>
        </rss>
        """;

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(rssXml, System.Text.Encoding.UTF8, "application/rss+xml")
        };
        using var client = new HttpClient(new FakeHttpMessageHandler(response));
        var scraper = new BusinessDayScraper(client);

        var articles = (await scraper.ScrapeAsync()).ToList();

        Assert.Single(articles);
        Assert.Equal("BusinessDay Nigeria", scraper.SourceName);
        Assert.Equal("businessday", scraper.SourceId);
        Assert.Equal("Business", scraper.DefaultCategory);

        var first = articles[0];
        Assert.Equal("CBN maintains benchmark monetary policy rate to curb inflation", first.Title);
        Assert.Equal("https://businessday.ng/news/article/cbn-maintains-rate/", first.Url);
        Assert.Equal("https://businessday.ng/wp-content/uploads/2026/09/cbn_building.jpg", first.ImageUrl);
        Assert.Equal("Business", first.Category);
    }

    [Fact]
    public void GuardianSitemapScraper_UsesNewsSitemapUrlAndValidFallbacks()
    {
        using var client = new HttpClient();
        var scraper = new GuardianSitemapScraper(client);

        Assert.Equal("guardian", scraper.SourceId);
        Assert.Equal("The Guardian Nigeria", scraper.SourceName);
        Assert.Equal("https://guardian.ng/news-sitemap.xml", scraper.SitemapUrl);
        Assert.NotNull(scraper.FallbackRssUrl);
        Assert.Contains("guardian.ng", scraper.FallbackRssUrl);
        Assert.NotNull(scraper.FallbackApiUrl);
        Assert.Contains("wp-json", scraper.FallbackApiUrl);
    }

    [Fact]
    public void ChannelsTvSitemapScraper_UsesNewsSitemapUrlAndValidFallbacks()
    {
        using var client = new HttpClient();
        var scraper = new ChannelsTvSitemapScraper(client);

        Assert.Equal("channelstv-sitemap", scraper.SourceId);
        Assert.Equal("Channels Television", scraper.SourceName);
        Assert.Equal("https://www.channelstv.com/news-sitemap.xml", scraper.SitemapUrl);
        Assert.Equal("https://www.channelstv.com/feed/", scraper.FallbackRssUrl);
    }

    [Fact]
    public void ChannelsTvSitemapScraper_ParsesNewsSitemapXmlCorrectly()
    {
        var sitemapXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:news="http://www.google.com/schemas/sitemap-news/0.9">
            <url>
                <loc>https://www.channelstv.com/2026/09/20/niger-miners-deaths-gov-bago-seeks-executive-order-to-ban-mining-in-some-areas/</loc>
                <news:news>
                    <news:publication>
                        <news:name>Channels Television</news:name>
                        <news:language>en</news:language>
                    </news:publication>
                    <news:publication_date>2026-09-20T20:31:53+00:00</news:publication_date>
                    <news:title><![CDATA[Niger Miners' Deaths: Gov Bago Seeks Executive Order To Ban Mining In Some Areas]]></news:title>
                </news:news>
            </url>
            <url>
                <loc>https://www.channelstv.com/2026/09/20/atiku-demands-explanation-for-tinubus-third-straight-unga-absence/</loc>
                <news:news>
                    <news:publication>
                        <news:name>Channels Television</news:name>
                        <news:language>en</news:language>
                    </news:publication>
                    <news:publication_date>2026-09-20T19:45:12+00:00</news:publication_date>
                    <news:title><![CDATA[Atiku Demands Explanation For Tinubu's Third Straight UNGA Absence]]></news:title>
                </news:news>
            </url>
        </urlset>
        """;

        using var client = new HttpClient();
        var scraper = new ChannelsTvSitemapScraper(client);
        var doc = XDocument.Parse(sitemapXml);
        var items = scraper.ParseSitemapDocument(doc);

        Assert.Equal(2, items.Count);
        Assert.Equal("Niger Miners' Deaths: Gov Bago Seeks Executive Order To Ban Mining In Some Areas", items[0].Title);
        Assert.Equal("https://www.channelstv.com/2026/09/20/niger-miners-deaths-gov-bago-seeks-executive-order-to-ban-mining-in-some-areas/", items[0].Url);
        Assert.Equal(new DateTime(2026, 9, 20, 20, 31, 53, DateTimeKind.Utc), items[0].PublishedAt);
        Assert.Equal("Channels Television", items[0].Source);

        Assert.Equal("Atiku Demands Explanation For Tinubu's Third Straight UNGA Absence", items[1].Title);
        Assert.Equal("https://www.channelstv.com/2026/09/20/atiku-demands-explanation-for-tinubus-third-straight-unga-absence/", items[1].Url);
    }
}

