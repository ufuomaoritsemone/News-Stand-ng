using NewsScraperService.Services;

namespace NewsScraperService.Scrapers;

public class DailyPostSitemapScraper : SitemapScraperBase
{
    public DailyPostSitemapScraper(HttpClient http, ArticleContentExtractor? extractor = null, UrlFrontierManager? frontier = null) 
        : base(http, extractor, frontier) { }

    public override string SourceId => "dailypost-sitemap";
    public override string SourceName => "Daily Post Nigeria";
    public override string SitemapUrl => "https://dailypost.ng/news-sitemap.xml";
    public override string? FallbackRssUrl => "https://dailypost.ng/feed/";
}
