using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;

public class ArticleTopic
{
    [Required]
    [MaxLength(64)]
    public string ArticleId { get; set; } = string.Empty;
    public Article? Article { get; set; }

    [Required]
    [MaxLength(64)]
    public string TopicId { get; set; } = string.Empty;
    public Topic? Topic { get; set; }

    public float Confidence { get; set; } = 1.0f;
}
