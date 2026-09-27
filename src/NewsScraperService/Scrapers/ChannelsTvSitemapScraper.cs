using NewsScraperService.Services;

namespace NewsScraperService.Scrapers;

public class ChannelsTvSitemapScraper : SitemapScraperBase
{
    public ChannelsTvSitemapScraper(HttpClient http, ArticleContentExtractor? extractor = null, UrlFrontierManager? frontier = null)
        : base(http, extractor, frontier) { }

    public override string SourceId => "channelstv-sitemap";
    public override string SourceName => "Channels Television";
    public override string SitemapUrl => "https://www.channelstv.com/news-sitemap.xml";
    public override string? FallbackRssUrl => "https://www.channelstv.com/feed/";
}
