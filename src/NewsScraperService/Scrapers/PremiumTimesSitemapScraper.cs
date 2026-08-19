using NewsScraperService.Services;

namespace NewsScraperService.Scrapers;

public class PremiumTimesSitemapScraper : SitemapScraperBase
{
    public PremiumTimesSitemapScraper(HttpClient http, ArticleContentExtractor? extractor = null, UrlFrontierManager? frontier = null) 
        : base(http, extractor, frontier) { }

    public override string SourceId => "premiumtimes-sitemap";
    public override string SourceName => "Premium Times";
    public override string SitemapUrl => "https://www.premiumtimesng.com/news-sitemap.xml";
    public override string? FallbackRssUrl => "https://www.premiumtimesng.com/feed";
}
