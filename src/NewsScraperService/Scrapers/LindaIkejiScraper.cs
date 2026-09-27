namespace NewsScraperService.Scrapers;

/// <summary>
/// Scrapes entertainment, lifestyle, and pop culture news from Linda Ikeji's Blog via its Atom/RSS syndication feed.
/// </summary>
public class LindaIkejiScraper : RssScraperBase
{
    public LindaIkejiScraper(HttpClient http) : base(http) { }

    public override string SourceId => "lindaikeji";
    public override string SourceName => "Linda Ikeji's Blog";
    public override string RssUrl => "https://www.lindaikejisblog.com/feed";
    public override string? DefaultCategory => "Entertainment";
}
