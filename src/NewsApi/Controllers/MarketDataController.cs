using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/data")]
public class MarketDataController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly ILogger<MarketDataController> _logger;

    public MarketDataController(NewsDbContext db, ILogger<MarketDataController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Returns summary cards for all evergreen data trackers (Naira FX, Fuel, Exams, Elections).
    /// </summary>
    [HttpGet("metrics")]
    [ResponseCache(Duration = 300)]
    public async Task<ActionResult<List<MarketDataDto>>> GetAllMetrics()
    {
        var records = await _db.MarketDataRecords
            .AsNoTracking()
            .ToListAsync();

        var dtos = records.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    /// <summary>
    /// Returns detailed tracker metric with historical series and FAQs.
    /// Canonical metric keys: "naira-fx", "fuel-prices", "jamb-waec", "elections".
    /// Also supports slug aliases: "naira-exchange-rate" -> "naira-fx", "fuel-price-nigeria" -> "fuel-prices".
    /// </summary>
    [HttpGet("{key}")]
    [ResponseCache(Duration = 180)]
    public async Task<ActionResult<MarketDataDto>> GetMetric(string key)
    {
        var normalizedKey = NormalizeKey(key);

        var record = await _db.MarketDataRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.MetricKey == normalizedKey);

        if (record == null)
        {
            return NotFound(new { message = $"Data tracker for '{key}' not found." });
        }

        return Ok(MapToDto(record));
    }

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;
        var clean = key.Trim().ToLowerInvariant();

        return clean switch
        {
            "naira-exchange-rate" or "naira" or "usd-ngn" => "naira-fx",
            "fuel-price-nigeria" or "fuel" or "petrol" or "fuel-price" => "fuel-prices",
            "jamb" or "waec" or "jamb-waec-news" or "exams" => "jamb-waec",
            "election" or "inec" or "election-updates" => "elections",
            _ => clean
        };
    }

    private static MarketDataDto MapToDto(MarketDataRecord record)
    {
        List<MarketDataPointDto> points = [];
        Dictionary<string, FxCurrencyRateDto>? currencies = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(record.HistoricalDataJson))
            {
                var trimmed = record.HistoricalDataJson.TrimStart();
                if (trimmed.StartsWith('{'))
                {
                    using var doc = JsonDocument.Parse(record.HistoricalDataJson);
                    if (doc.RootElement.TryGetProperty("points", out var pointsEl))
                    {
                        points = JsonSerializer.Deserialize<List<MarketDataPointDto>>(pointsEl.GetRawText()) ?? [];
                    }
                    if (doc.RootElement.TryGetProperty("currencies", out var currEl))
                    {
                        currencies = JsonSerializer.Deserialize<Dictionary<string, FxCurrencyRateDto>>(
                            currEl.GetRawText(),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                }
                else
                {
                    points = JsonSerializer.Deserialize<List<MarketDataPointDto>>(record.HistoricalDataJson) ?? [];
                }
            }
        }
        catch { }

        List<FaqItemDto> faqs = [];
        try
        {
            if (!string.IsNullOrWhiteSpace(record.FaqsJson))
            {
                faqs = JsonSerializer.Deserialize<List<FaqItemDto>>(record.FaqsJson) ?? [];
            }
        }
        catch { }

        return new MarketDataDto
        {
            MetricKey = record.MetricKey,
            Title = record.Title,
            PrimaryValue = record.PrimaryValue,
            SecondaryValue = record.SecondaryValue,
            Change24h = record.Change24h,
            Unit = record.Unit,
            SourceAttribution = record.SourceAttribution,
            SummaryMarkdown = record.SummaryMarkdown,
            LastUpdated = record.LastUpdated,
            HistoricalPoints = points,
            Faqs = faqs,
            Currencies = currencies
        };
    }
}
