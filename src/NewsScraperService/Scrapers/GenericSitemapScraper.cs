using NewsScraperService.Services;

namespace NewsScraperService.Scrapers;

public class GenericSitemapScraper : SitemapScraperBase
{
    private readonly string _sourceId;
    private readonly string _sourceName;
    private readonly string _sitemapUrl;
    private readonly string? _fallbackRssUrl;

    public GenericSitemapScraper(
        HttpClient http, 
        string sourceId, 
        string sourceName, 
        string sitemapUrl, 
        string? fallbackRssUrl = null,
        ArticleContentExtractor? extractor = null, 
        UrlFrontierManager? frontier = null) 
        : base(http, extractor, frontier)
    {
        _sourceId = sourceId;
        _sourceName = sourceName;
        _sitemapUrl = sitemapUrl;
        _fallbackRssUrl = fallbackRssUrl;
    }

    public override string SourceId => _sourceId;
    public override string SourceName => _sourceName;
    public override string SitemapUrl => _sitemapUrl;
    public override string? FallbackRssUrl => _fallbackRssUrl;
}
