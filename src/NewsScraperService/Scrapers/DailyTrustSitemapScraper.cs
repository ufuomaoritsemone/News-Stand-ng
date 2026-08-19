using NewsScraperService.Services;

namespace NewsScraperService.Scrapers;

public class DailyTrustSitemapScraper : SitemapScraperBase
{
    public DailyTrustSitemapScraper(HttpClient http, ArticleContentExtractor? extractor = null, UrlFrontierManager? frontier = null) 
        : base(http, extractor, frontier) { }

    public override string SourceId => "dailytrust-sitemap";
    public override string SourceName => "Daily Trust";
    public override string SitemapUrl => "https://dailytrust.com/news-sitemap.xml";
    public override string? FallbackRssUrl => "https://dailytrust.com/feed";
}
