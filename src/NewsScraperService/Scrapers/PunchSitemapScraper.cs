using NewsScraperService.Services;

namespace NewsScraperService.Scrapers;

public class PunchSitemapScraper : SitemapScraperBase
{
    public PunchSitemapScraper(HttpClient http, ArticleContentExtractor? extractor = null, UrlFrontierManager? frontier = null) 
        : base(http, extractor, frontier) { }

    public override string SourceId => "punch-sitemap";
    public override string SourceName => "The Punch";
    public override string SitemapUrl => "https://punchng.com/news-sitemap.xml";
    public override string? FallbackRssUrl => "https://punchng.com/feed/";
}
