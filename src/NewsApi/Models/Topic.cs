using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;

public enum TopicType
{
    Person = 0,
    Organization = 1,
    Place = 2,
    Theme = 3
}

public class Topic
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    [MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public TopicType Type { get; set; } = TopicType.Theme;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ArticleTopic> ArticleTopics { get; set; } = [];
}
