using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace NewsApi.Models;

/// <summary>
/// Stores machine learning and AI-generated article enrichment data (key takeaways, timeline facts, FAQ pairs).
/// </summary>
public class ArticleEnrichment
{
    [Key]
    public string ArticleId { get; set; } = string.Empty;

    public Article? Article { get; set; }

    /// <summary>
    /// Serialized JSON array of 3 bullet key takeaways.
    /// </summary>
    public string KeyTakeawaysJson { get; set; } = "[]";

    /// <summary>
    /// Serialized JSON array of TimelineFactDto objects.
    /// </summary>
    public string TimelineFactsJson { get; set; } = "[]";

    /// <summary>
    /// Serialized JSON array of FaqItemDto objects.
    /// </summary>
    public string FaqsJson { get; set; } = "[]";

    /// <summary>
    /// Serialized JSON representing structured executive summary of the full article body.
    /// </summary>
    public string? FullSummaryJson { get; set; }

    /// <summary>
    /// The engine that generated this enrichment ("TextRank-Extractive" or "Gemini-3.1-Flash-Lite").
    /// </summary>
    [MaxLength(50)]
    public string Engine { get; set; } = "TextRank-Extractive";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    [NotMapped]
    public List<string> KeyTakeaways
    {
        get => DeserializeJson<List<string>>(KeyTakeawaysJson) ?? [];
        set => KeyTakeawaysJson = JsonSerializer.Serialize(value);
    }

    [NotMapped]
    public List<TimelineFactDto> TimelineFacts
    {
        get => DeserializeJson<List<TimelineFactDto>>(TimelineFactsJson) ?? [];
        set => TimelineFactsJson = JsonSerializer.Serialize(value);
    }

    [NotMapped]
    public List<FaqItemDto> Faqs
    {
        get => DeserializeJson<List<FaqItemDto>>(FaqsJson) ?? [];
        set => FaqsJson = JsonSerializer.Serialize(value);
    }

    [NotMapped]
    public FullSummaryPayload? FullSummary
    {
        get => DeserializeJson<FullSummaryPayload>(FullSummaryJson ?? string.Empty);
        set => FullSummaryJson = value != null ? JsonSerializer.Serialize(value) : null;
    }

    private static T? DeserializeJson<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Structured narrative synthesis of the entire article body.
/// </summary>
public class FullSummaryPayload
{
    public string ExecutiveOverview { get; set; } = string.Empty;
    public List<string> ContextAndBackground { get; set; } = [];
    public List<string> KeyFiguresAndEntities { get; set; } = [];
    public string OutlookOrNextSteps { get; set; } = string.Empty;
    public int ReadingTimeSeconds { get; set; } = 45;
}

