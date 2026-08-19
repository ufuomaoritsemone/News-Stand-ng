using NewsScraperService.Models;

namespace NewsScraperService.Scrapers;

public class GuardianScraper : RssScraperBase
{
    public GuardianScraper(HttpClient http) : base(http) { }

    public override string SourceId => "guardian";
    public override string SourceName => "The Guardian (Nigeria)";
    public override string RssUrl => "https://guardian.ng/?format=rss"; // Guardian Nigeria RSS
}
