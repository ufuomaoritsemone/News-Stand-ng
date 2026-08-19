namespace NigerianNewsGrid.Client.Models;

/// <summary>
/// Result container holding related articles from the archive, video stories from YouTube, and social posts/tweets.
/// </summary>
public sealed record RelatedStoriesResult
{
    public List<BriefingItem> Articles { get; init; } = [];
    public List<VideoStoryItem> Videos { get; init; } = [];
    public List<SocialPostItem> SocialPosts { get; init; } = [];
    public int TotalCount => Articles.Count + Videos.Count + SocialPosts.Count;
}
