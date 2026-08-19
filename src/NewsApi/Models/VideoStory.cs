using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;

/// <summary>
/// A specific video news story/clip published by a monitored YouTube news channel.
/// Automatically ingested via YouTube channel RSS feeds.
/// </summary>
public class VideoStory
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string Duration { get; set; } = "12:00";
    public string Category { get; set; } = "News";
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Trending metadata
    public bool IsTrending { get; set; } = false;
    public int? TrendingRank { get; set; }
    public long ViewCount { get; set; }
    public long LikeCount { get; set; }
}
