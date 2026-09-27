using NewsScraperService.Services;

namespace NewsScraperService.Scrapers;

public class GuardianSitemapScraper : SitemapScraperBase
{
    public GuardianSitemapScraper(HttpClient http, ArticleContentExtractor? extractor = null, UrlFrontierManager? frontier = null)
        : base(http, extractor, frontier) { }

    public override string SourceId => "guardian";
    public override string SourceName => "The Guardian Nigeria";
    public override string SitemapUrl => "https://guardian.ng/news-sitemap.xml";
    public override string? FallbackRssUrl => "https://news.google.com/rss/search?q=site:guardian.ng&hl=en-NG&gl=NG&ceid=NG:en";
    public override string? FallbackApiUrl => "https://guardian.ng/wp-json/wp/v2/posts?per_page=20&_embed=true";
}
