using NewsScraperService.Services;

namespace NewsScraperService.Scrapers;

public class VanguardSitemapScraper : SitemapScraperBase
{
    public VanguardSitemapScraper(HttpClient http, ArticleContentExtractor? extractor = null, UrlFrontierManager? frontier = null) 
        : base(http, extractor, frontier) { }

    public override string SourceId => "vanguard-sitemap";
    public override string SourceName => "Vanguard";
    public override string SitemapUrl => "https://www.vanguardngr.com/news-sitemap.xml";
    public override string? FallbackRssUrl => "https://www.vanguardngr.com/feed/";
}
