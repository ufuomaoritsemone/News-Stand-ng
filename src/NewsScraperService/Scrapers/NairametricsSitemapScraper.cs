using NewsScraperService.Services;

namespace NewsScraperService.Scrapers;

public class NairametricsSitemapScraper : SitemapScraperBase
{
    public NairametricsSitemapScraper(HttpClient http, ArticleContentExtractor? extractor = null, UrlFrontierManager? frontier = null)
        : base(http, extractor, frontier) { }

    public override string SourceId => "nairametrics-sitemap";
    public override string SourceName => "Nairametrics";
    public override string SitemapUrl => "https://nairametrics.com/news-sitemap.xml";
    public override string? FallbackRssUrl => "https://nairametrics.com/feed/";
}
