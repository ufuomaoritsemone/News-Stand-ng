using NewsScraperService.Models;

namespace NewsScraperService.Scrapers;

public class GenericRssScraper : RssScraperBase
{
    private readonly string _sourceId;
    private readonly string _sourceName;
    private readonly string _rssUrl;

    public GenericRssScraper(HttpClient http, string sourceId, string sourceName, string rssUrl) : base(http)
    {
        _sourceId = sourceId;
        _sourceName = sourceName;
        _rssUrl = rssUrl;
    }

    public override string SourceId => _sourceId;
    public override string SourceName => _sourceName;
    public override string RssUrl => _rssUrl;
}
