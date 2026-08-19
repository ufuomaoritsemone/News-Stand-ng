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
    public List<ArticleIngestItem> Articles { get; set; } = new();
}

public class ArticleIngestItem
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Content { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }
    public string? Source { get; set; }
    public string? Category { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? AudioUrl { get; set; }
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
