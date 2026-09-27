namespace NewsScraperService.Scrapers;

/// <summary>
/// Scrapes financial, economy, capital market, and business intelligence news from BusinessDay Nigeria.
/// </summary>
public class BusinessDayScraper : RssScraperBase
{
    public BusinessDayScraper(HttpClient http) : base(http) { }

    public override string SourceId => "businessday";
    public override string SourceName => "BusinessDay Nigeria";
    public override string RssUrl => "https://businessday.ng/feed/";
    public override string? DefaultCategory => "Business";
}
