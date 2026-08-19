using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;

public class AudioAsset
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? ArticleId { get; set; }
    public string Url { get; set; } = string.Empty; // Blob or CDN URL to MP3
    public TimeSpan Duration { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
