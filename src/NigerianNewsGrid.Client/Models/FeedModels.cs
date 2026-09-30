using System.Text.Json.Serialization;

namespace NigerianNewsGrid.Client.Models;

/// <summary>
/// Immutable record representing a specific YouTube video news story for deserialization.
/// </summary>
public sealed record VideoStoryItem
{
    public string Id { get; init; } = string.Empty;
    public string VideoId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string VideoUrl { get; init; } = string.Empty;
    public string ThumbnailUrl { get; init; } = string.Empty;
    public string ChannelName { get; init; } = string.Empty;
    public string ChannelId { get; init; } = string.Empty;
    public string Duration { get; init; } = "12:00";
    public string Category { get; init; } = "News";
    public DateTime PublishedAt { get; init; } = DateTime.UtcNow;
    public bool IsTrending { get; init; } = false;
    public int? TrendingRank { get; init; }
    public long ViewCount { get; init; } = 0;
    public long LikeCount { get; init; } = 0;

    [JsonIgnore]
    public string ViewCountText
    {
        get
        {
            if (ViewCount > 0)
            {
                var formatted = ViewCount >= 1000 ? $"{ViewCount / 1000.0:F1}K" : ViewCount.ToString();
                return IsTrending ? $"🔥 {formatted} views" : $"📺 {formatted} views";
            }
            return IsTrending ? "🔥 Trending" : "📺 Latest Broadcast";
        }
    }

    /// <summary>
    /// Computes the best available thumbnail URL: returns ThumbnailUrl if present and HTTP(S),
    /// otherwise falls back to deterministic YouTube CDN (hqdefault.jpg), or a high-quality fallback image.
    /// </summary>
    [JsonIgnore]
    public string DisplayThumbnailUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ThumbnailUrl) &&
                (ThumbnailUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                 ThumbnailUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
            {
                var trimmed = ThumbnailUrl.Trim();
                if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = "https://" + trimmed["http://".Length..];
                }
                return trimmed;
            }

            if (!string.IsNullOrWhiteSpace(VideoId))
            {
                return $"https://i.ytimg.com/vi/{VideoId.Trim()}/hqdefault.jpg";
            }

            return "https://images.unsplash.com/photo-1585829365295-ab7cd400c167?w=600&auto=format&fit=crop&q=80";
        }
    }
}

/// <summary>
/// Immutable record representing a specific tweet or breaking social story for deserialization.
/// </summary>
public sealed record SocialPostItem
{
    public string Id { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public string AuthorHandle { get; init; } = string.Empty;
    public string AuthorAvatarUrl { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string PostUrl { get; init; } = string.Empty;
    public string? MediaUrl { get; init; }
    public string Category { get; init; } = "Breaking";
    public int LikesCount { get; init; } = 0;
    public int RetweetsCount { get; init; } = 0;
    public DateTime PublishedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Legacy model for video channel metadata.
/// </summary>
public sealed record VideoFeedItem
{
    public string Id { get; init; } = string.Empty;
    public string ChannelName { get; init; } = string.Empty;
    public string YoutubeChannelId { get; init; } = string.Empty;
    public string ThumbnailUrl { get; init; } = string.Empty;
    public string ChannelUrl { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Legacy model for social handle metadata.
/// </summary>
public sealed record SocialFeedItem
{
    public string Id { get; init; } = string.Empty;
    public string Handle { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ProfileUrl { get; init; } = string.Empty;
    public string AvatarUrl { get; init; } = string.Empty;
    public string Bio { get; init; } = string.Empty;
    public string Category { get; init; } = "Media";
}

/// <summary>
/// User experience feedback submission DTOs for client-side API interaction.
/// </summary>
public sealed record FeedbackRequestDto(
    int Rating,
    string? Category,
    string Message,
    string? UserEmail = null,
    string? UserName = null,
    string? AppVersion = null,
    string? Platform = null
);

public sealed record FeedbackResponseDto(
    bool Success,
    string Message,
    string? FeedbackId = null
);

/// <summary>
/// Lightweight delta record for reconciling out-of-band article changes (such as category reclassifications in Admin Dashboard)
/// between the central server and client SQLite databases.
/// </summary>
public sealed record ArticleDeltaDto
{
    public string Id { get; init; } = string.Empty;
    public string Category { get; init; } = "General";
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}

