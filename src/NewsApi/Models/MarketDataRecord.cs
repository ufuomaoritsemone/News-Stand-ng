namespace NewsApi.Models;

public class MarketDataRecord
{
    public int Id { get; set; }

    /// <summary>
    /// Unique identifier for the evergreen tracker metric:
    /// e.g. "naira-fx", "fuel-prices", "jamb-waec", "elections".
    /// </summary>
    public string MetricKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string PrimaryValue { get; set; } = string.Empty;

    public string? SecondaryValue { get; set; }

    public string? Change24h { get; set; }

    public string Unit { get; set; } = string.Empty;

    public string SourceAttribution { get; set; } = string.Empty;

    /// <summary>
    /// Serialized JSON array of historical points or regional breakdown.
    /// </summary>
    public string HistoricalDataJson { get; set; } = "[]";

    /// <summary>
    /// Serialized JSON array of FaqItemDto objects.
    /// </summary>
    public string FaqsJson { get; set; } = "[]";

    public string SummaryMarkdown { get; set; } = string.Empty;

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
