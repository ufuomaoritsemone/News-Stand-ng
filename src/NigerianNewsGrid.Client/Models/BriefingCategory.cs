namespace NigerianNewsGrid.Client.Models;

/// <summary>
/// A grouping of top articles within a news category (e.g. "Politics", "Sports").
/// Immutable record for deserialization — per C# best practices, DTOs should be records.
/// </summary>
public sealed record BriefingCategory
{
    public string Category { get; init; } = "General";
    public List<BriefingItem> Top { get; init; } = [];
}

public sealed record BriefingItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string? Content { get; init; }
    public string? Url { get; init; }
    public string? ImageUrl { get; init; }
    public string? Source { get; init; }
    public string? Category { get; init; }
    public DateTime? PublishedAt { get; init; }
    public string? Language { get; init; }
    public string? VoiceName { get; init; }
    public string? AudioUrl { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string TimeAgoText => PublishedAt.HasValue
        ? FormatTimeAgo(PublishedAt.Value)
        : string.Empty;

    private static string FormatTimeAgo(DateTime utcTime)
    {
        var diff = DateTime.UtcNow - utcTime;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return utcTime.ToLocalTime().ToString("MMM d");
    }
}
