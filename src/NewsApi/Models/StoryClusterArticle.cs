using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;

public class StoryClusterArticle
{
    [Required]
    [MaxLength(64)]
    public string ClusterId { get; set; } = string.Empty;
    public StoryCluster? Cluster { get; set; }

    [Required]
    [MaxLength(64)]
    public string ArticleId { get; set; } = string.Empty;
    public Article? Article { get; set; }

    public float SimilarityScore { get; set; } = 1.0f;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
