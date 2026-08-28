using NewsScraperService.Services;

namespace NewsScraperService.Scrapers;

public class GuardianSitemapScraper : SitemapScraperBase
{
    public GuardianSitemapScraper(HttpClient http, ArticleContentExtractor? extractor = null, UrlFrontierManager? frontier = null)
        : base(http, extractor, frontier) { }

    public override string SourceId => "guardian";
    public override string SourceName => "The Guardian Nigeria";
    public override string SitemapUrl => "https://guardian.ng/sitemap.xml";
    public override string? FallbackRssUrl => "https://guardian.ng/feed/";
}
