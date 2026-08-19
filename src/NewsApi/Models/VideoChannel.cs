using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;

/// <summary>
/// A YouTube video channel that surfaces Nigerian news content.
/// Managed via Admin Dashboard; served to mobile app for the "Video" feed section.
/// </summary>
public class VideoChannel
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string YoutubeChannelId { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string ChannelUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
