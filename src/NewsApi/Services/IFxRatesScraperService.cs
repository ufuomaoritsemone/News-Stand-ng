using NewsApi.Models;

namespace NewsApi.Services;

public sealed class FxScrapeResult
{
    public Dictionary<string, FxCurrencyRateDto> Currencies { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<MarketDataPointDto> HistoricalPoints { get; set; } = [];
    public string PrimaryValue { get; set; } = string.Empty;
    public string SecondaryValue { get; set; } = string.Empty;
    public string Change24h { get; set; } = "+0.35%";
    public string SourceAttribution { get; set; } = "Central Bank of Nigeria (NAFEM) & Lagos Parallel Market Desk (AbokiForex)";
    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;
}

public interface IFxRatesScraperService
{
    Task<FxScrapeResult> ScrapeLatestFxRatesAsync(CancellationToken ct = default);
}
