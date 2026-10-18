using SQLite;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Models;

/// <summary>
/// SQLite entity representing a locally cached news story with a 14-day rolling retention policy.
/// Preserves bookmarks and read states across eviction sweeps.
/// </summary>
[Table("CachedStories")]
public class CachedStoryEntity
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string Category { get; set; } = "General";

    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Content { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }
    public string? Source { get; set; }

    [Indexed]
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    public string? Language { get; set; }
    public string? VoiceName { get; set; }
    public string? AudioUrl { get; set; }
    public string? Author { get; set; }
    public string? ContentType { get; set; } = "News";

    // Direct Sponsorship fields
    public bool IsSponsored { get; set; }
    public string? SponsorName { get; set; }
    public string? SponsorUrl { get; set; }
    public bool IsPinned { get; set; }
    public int? TargetPosition { get; set; }
    public int PriorityWeight { get; set; } = 1;
    public bool IsAdMobPlaceholder { get; set; }
    public string PlacementZone { get; set; } = "MainPage_InFeed";
    public string TargetPlatform { get; set; } = "All";

    // AI Summary & Enrichment fields
    public bool HasAiSummary { get; set; }
    public string? AiSummary { get; set; }
    public string? AiSummaryEngine { get; set; }
    public string? KeyTakeawaysJson { get; set; }
    public int CommentCount { get; set; }

    // Local tracking state
    [Indexed]
    public bool IsBookmarked { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CachedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Convert to domain/client BriefingItem.
    /// </summary>
    public BriefingItem ToBriefingItem() => new()
    {
        Id = Id,
        Title = Title,
        Summary = Summary,
        Content = Content,
        Url = Url,
        ImageUrl = string.IsNullOrWhiteSpace(ImageUrl) ? "https://images.unsplash.com/photo-1585829365295-ab7cd400c167?w=600&auto=format&fit=crop&q=80" : ImageUrl,
        Source = Source,
        Category = Category,
        PublishedAt = PublishedAt,
        Language = Language,
        VoiceName = VoiceName,
        AudioUrl = AudioUrl,
        Author = Author,
        ContentType = ContentType,
        IsSponsored = IsSponsored,
        SponsorName = SponsorName,
        SponsorUrl = SponsorUrl,
        IsPinned = IsPinned,
        TargetPosition = TargetPosition,
        PriorityWeight = PriorityWeight,
        IsAdMobPlaceholder = IsAdMobPlaceholder,
        PlacementZone = PlacementZone,
        TargetPlatform = TargetPlatform,
        HasAiSummary = HasAiSummary,
        AiSummary = AiSummary,
        AiSummaryEngine = AiSummaryEngine,
        KeyTakeaways = !string.IsNullOrWhiteSpace(KeyTakeawaysJson)
            ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(KeyTakeawaysJson) ?? []
            : [],
        CommentCount = CommentCount
    };

    /// <summary>
    /// Factory from domain/client BriefingItem.
    /// </summary>
    public static CachedStoryEntity FromBriefingItem(BriefingItem item, string fallbackCategory = "General") => new()
    {
        Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString() : item.Id,
        Category = string.IsNullOrWhiteSpace(item.Category) ? fallbackCategory : item.Category,
        Title = item.Title ?? string.Empty,
        Summary = item.Summary,
        Content = item.Content,
        Url = item.Url,
        ImageUrl = item.ImageUrl,
        Source = item.Source,
        PublishedAt = item.PublishedAt ?? DateTime.UtcNow,
        Language = item.Language,
        VoiceName = item.VoiceName,
        AudioUrl = item.AudioUrl,
        Author = item.Author,
        ContentType = item.ContentType ?? "News",
        IsSponsored = item.IsSponsored,
        SponsorName = item.SponsorName,
        SponsorUrl = item.SponsorUrl,
        IsPinned = item.IsPinned,
        TargetPosition = item.TargetPosition,
        PriorityWeight = item.PriorityWeight,
        IsAdMobPlaceholder = item.IsAdMobPlaceholder,
        PlacementZone = item.PlacementZone,
        TargetPlatform = item.TargetPlatform,
        HasAiSummary = item.HasAiSummary,
        AiSummary = item.AiSummary,
        AiSummaryEngine = item.AiSummaryEngine,
        KeyTakeawaysJson = item.KeyTakeaways.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(item.KeyTakeaways) : null,
        CommentCount = item.CommentCount,
        CachedAtUtc = DateTime.UtcNow
    };
}
