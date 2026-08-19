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
