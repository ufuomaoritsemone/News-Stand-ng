using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;

public class Article
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Content { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }
    public string? Source { get; set; }
    public string? Category { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? AudioUrl { get; set; }

    // Author & Editorial/Opinion Content Classification
    public string? Author { get; set; }
    public string? ContentType { get; set; } = "News";

    // SEO, Permalinks & Structured Data
    [Required]
    [MaxLength(120)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Excerpt { get; set; }

    [MaxLength(70)]
    public string? SeoTitle { get; set; }

    [MaxLength(160)]
    public string? SeoDescription { get; set; }

    // Navigation properties
    public ICollection<ArticleTopic> ArticleTopics { get; set; } = [];
    public ArticleEmbedding? Embedding { get; set; }
    public ICollection<StoryClusterArticle> StoryClusterArticles { get; set; } = [];
    public ArticleEnrichment? Enrichment { get; set; }

    // Direct In-House Sponsorship Engine Fields
    public bool IsSponsored { get; set; } = false;
    public string? SponsorName { get; set; }
    public string? SponsorUrl { get; set; }
    public DateTime? CampaignExpiresAt { get; set; }
    public bool IsPinned { get; set; } = false;
    public int? TargetPosition { get; set; }
    public int PriorityWeight { get; set; } = 1;
    public int ImpressionCount { get; set; } = 0;
    public int ClickCount { get; set; } = 0;
    public string PlacementZone { get; set; } = "MainPage_InFeed";
    public string TargetPlatform { get; set; } = "All";

    // Delta tracking timestamp for client cache reconciliation
    public DateTime? UpdatedAt { get; set; }
}
