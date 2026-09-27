using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;


public class ArticleDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Content { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }
    public string Source { get; set; } = "General News";
    public string Category { get; set; } = "General";
    public DateTime PublishedAt { get; set; }
    public string? AudioUrl { get; set; }
    public string? Author { get; set; }
    public string? ContentType { get; set; } = "News";

    // Direct In-House Sponsorship Engine Fields
    public bool IsSponsored { get; set; }
    public string? SponsorName { get; set; }
    public string? SponsorUrl { get; set; }
    public DateTime? CampaignExpiresAt { get; set; }
    public bool IsPinned { get; set; }
    public int? TargetPosition { get; set; }
    public int PriorityWeight { get; set; } = 1;
    public int ImpressionCount { get; set; }
    public int ClickCount { get; set; }
    public double ClickThroughRate => ImpressionCount > 0 ? Math.Round(((double)ClickCount / ImpressionCount) * 100, 2) : 0;
    public bool IsActive => IsSponsored && (CampaignExpiresAt == null || CampaignExpiresAt > DateTime.UtcNow);
}

public class CreateSponsoredArticleRequest
{
    [Required]
    [MaxLength(1000)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string? Summary { get; set; }

    public string? Content { get; set; }

    [MaxLength(2048)]
    public string? Url { get; set; }

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    [Required]
    [MaxLength(200)]
    public string SponsorName { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? SponsorUrl { get; set; }

    [MaxLength(100)]
    public string Category { get; set; } = "Business";

    public DateTime? CampaignExpiresAt { get; set; }

    public bool IsPinned { get; set; } = true;

    public int? TargetPosition { get; set; }

    public int PriorityWeight { get; set; } = 1;
}

public class UpdateSponsoredArticleRequest
{
    [MaxLength(1000)]
    public string? Title { get; set; }

    [MaxLength(5000)]
    public string? Summary { get; set; }

    public string? Content { get; set; }

    [MaxLength(2048)]
    public string? Url { get; set; }

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    [MaxLength(200)]
    public string? SponsorName { get; set; }

    [MaxLength(2048)]
    public string? SponsorUrl { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public DateTime? CampaignExpiresAt { get; set; }

    public bool? IsPinned { get; set; }

    public bool? IsSponsored { get; set; }

    public int? TargetPosition { get; set; }

    public int? PriorityWeight { get; set; }
}

public class PagedResponse<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
}

public class IngestArticlesRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one article must be provided.")]
    public List<ArticleIngestItem> Articles { get; set; } = [];
}

public class ArticleIngestItem
{
    [Required]
    [MaxLength(1000, ErrorMessage = "Title must not exceed 1000 characters.")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string? Summary { get; set; }

    public string? Content { get; set; }

    [MaxLength(2048, ErrorMessage = "URL must not exceed 2048 characters.")]
    [Url(ErrorMessage = "Url must be a valid absolute URL.")]
    public string? Url { get; set; }

    [MaxLength(2048)]
    public string? ImageUrl { get; set; }

    [MaxLength(200)]
    public string? Source { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public DateTime? PublishedAt { get; set; }

    [MaxLength(2048)]
    public string? AudioUrl { get; set; }

    [MaxLength(200)]
    public string? Author { get; set; }

    [MaxLength(100)]
    public string? ContentType { get; set; } = "News";
}

public class IngestArticlesResponse
{
    public int IngestedCount { get; set; }
    public int SkippedDuplicateCount { get; set; }
}

public class ErrorResponseDto
{
    public ErrorPayload Error { get; set; } = new();
}

public class ErrorPayload
{
    public string Code { get; set; } = "INTERNAL_ERROR";
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string TraceId { get; set; } = string.Empty;
    public List<ErrorDetail> Details { get; set; } = new();
}

public class ErrorDetail
{
    public string Field { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
}
