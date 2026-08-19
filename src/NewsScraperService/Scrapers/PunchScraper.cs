using NewsScraperService.Models;

namespace NewsScraperService.Scrapers;

public class PunchScraper : RssScraperBase
{
    public PunchScraper(HttpClient http) : base(http) { }

    public override string SourceId => "punch";
    public override string SourceName => "The Punch";
    public override string RssUrl => "https://punchng.com/feed/";
}
