using NewsScraperService.Services;

namespace NewsScraperService.Scrapers;

public class TheCableSitemapScraper : SitemapScraperBase
{
    public TheCableSitemapScraper(HttpClient http, ArticleContentExtractor? extractor = null, UrlFrontierManager? frontier = null) 
        : base(http, extractor, frontier) { }

    public override string SourceId => "thecable-sitemap";
    public override string SourceName => "TheCable";
    public override string SitemapUrl => "https://www.thecable.ng/news-sitemap.xml";
    public override string? FallbackRssUrl => "https://www.thecable.ng/feed";
}
