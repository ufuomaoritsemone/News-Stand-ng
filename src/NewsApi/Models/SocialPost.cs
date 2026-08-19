using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;

/// <summary>
/// A specific tweet or social media story from a monitored Nigerian news outlet or commentator.
/// </summary>
public class SocialPost
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorHandle { get; set; } = string.Empty;
    public string AuthorAvatarUrl { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string PostUrl { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string Category { get; set; } = "Breaking";
    public int LikesCount { get; set; } = 0;
    public int RetweetsCount { get; set; } = 0;
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
