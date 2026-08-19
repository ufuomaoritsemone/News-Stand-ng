using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;

/// <summary>
/// A Twitter/X social media handle for Nigerian news outlets or commentators.
/// Managed via Admin Dashboard; served to mobile app for the "Socials" feed section.
/// </summary>
public class SocialHandle
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Category { get; set; } = "Media";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
