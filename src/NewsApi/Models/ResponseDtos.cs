namespace NewsApi.Models;

/// <summary>
/// Response DTO for news source metadata, decoupled from database entity schema.
/// </summary>
public class SourceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? RssUrl { get; set; }
    public string? SitemapUrl { get; set; }
    public string? ScraperType { get; set; } = "Hybrid";
}

/// <summary>
/// Response DTO for registered YouTube video channels.
/// </summary>
public class VideoChannelDto
{
    public string Id { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string YoutubeChannelId { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string ChannelUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Response DTO for video news stories and clips.
/// </summary>
public class VideoStoryDto
{
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
    public DateTime PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsTrending { get; set; }
    public int? TrendingRank { get; set; }
    public long ViewCount { get; set; }
    public long LikeCount { get; set; }
}

/// <summary>
/// Response DTO for monitored social media handles.
/// </summary>
public class SocialHandleDto
{
    public string Id { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Category { get; set; } = "Media";
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Response DTO for breaking social posts and tweets.
/// </summary>
public class SocialPostDto
{
    public string Id { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorHandle { get; set; } = string.Empty;
    public string AuthorAvatarUrl { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string PostUrl { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string Category { get; set; } = "Breaking";
    public int LikesCount { get; set; }
    public int RetweetsCount { get; set; }
    public DateTime PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
