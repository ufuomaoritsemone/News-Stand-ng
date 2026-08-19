using NewsScraperService.Models;

namespace NewsScraperService.Scrapers;

public class PremiumTimesScraper : RssScraperBase
{
    public PremiumTimesScraper(HttpClient http) : base(http) { }

    public override string SourceId => "premiumtimes";
    public override string SourceName => "Premium Times";
    public override string RssUrl => "https://www.premiumtimesng.com/feed/";
}
