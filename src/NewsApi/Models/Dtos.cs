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
    public DateTime? PublishedAt { get; set; }
    public string? AudioUrl { get; set; }
    public string? Author { get; set; }
    public string? ContentType { get; set; } = "News";
    public int CommentCount { get; set; } = 0;

    // SEO, Permalinks & Structured Data
    public string Slug { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }

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
    public string PlacementZone { get; set; } = "MainPage_InFeed";
    public string TargetPlatform { get; set; } = "All";

    // AI Enrichment / Summary Metadata
    public bool HasAiSummary { get; set; }
    public string? AiSummaryEngine { get; set; }
    public string? AiSummary { get; set; }
    public ArticleEnrichmentDto? Enrichment { get; set; }
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

    [MaxLength(50)]
    public string PlacementZone { get; set; } = "MainPage_InFeed";

    [MaxLength(20)]
    public string TargetPlatform { get; set; } = "All";
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

    [MaxLength(50)]
    public string? PlacementZone { get; set; }

    [MaxLength(20)]
    public string? TargetPlatform { get; set; }
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

    public List<ArticleTopicIngestItem>? Topics { get; set; }
}

public class ArticleTopicIngestItem
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Type { get; set; } = "Theme";
    public string? Description { get; set; }
    public float Confidence { get; set; } = 1.0f;
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

// ── SEO & Public Permalinks DTOs ─────────────────────────────────────────────

public class ArticleDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    public string? Source { get; set; }
    public string? SourceUrl { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? Author { get; set; }
    public string? Category { get; set; }
    public string? ContentType { get; set; } = "News";
    public string? AudioUrl { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public bool IsSponsored { get; set; }
    public string? SponsorName { get; set; }
    public string? SponsorUrl { get; set; }
    public string PlacementZone { get; set; } = "MainPage_InFeed";
    public string TargetPlatform { get; set; } = "All";
    public string? RedirectSlug { get; set; }
    public List<TopicDto> Topics { get; set; } = [];
    public List<ArticleDto> RelatedArticles { get; set; } = [];
    public ArticleEnrichmentDto? Enrichment { get; set; }
}

public class ArticleEnrichmentDto
{
    public List<string> KeyTakeaways { get; set; } = [];
    public List<TimelineFactDto> TimelineFacts { get; set; } = [];
    public List<FaqItemDto> Faqs { get; set; } = [];
    public FullSummaryDto? FullSummary { get; set; }
    public string Engine { get; set; } = "TextRank-Extractive";
    public DateTime? UpdatedAt { get; set; }
}

public class FullSummaryDto
{
    public string ExecutiveOverview { get; set; } = string.Empty;
    public List<string> ContextAndBackground { get; set; } = [];
    public List<string> KeyFiguresAndEntities { get; set; } = [];
    public string OutlookOrNextSteps { get; set; } = string.Empty;
    public int ReadingTimeSeconds { get; set; } = 45;
}


public class TimelineFactDto
{
    public string Fact { get; set; } = string.Empty;
    public string? DateText { get; set; }
    public string? DateOrTime { get => DateText; set => DateText = value; }
    public string? Entity { get; set; }

    public TimelineFactDto() { }
    public TimelineFactDto(string fact, string? dateText = null, string? entity = null)
    {
        Fact = fact;
        DateText = dateText;
        Entity = entity;
    }
}

public class FaqItemDto
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;

    public FaqItemDto() { }
    public FaqItemDto(string question, string answer)
    {
        Question = question;
        Answer = answer;
    }
}

public class TopicDto
{
    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "Theme";
    public string? Description { get; set; }
    public int ArticleCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SitemapEntryDto
{
    public string Loc { get; set; } = string.Empty;
    public DateTime? LastMod { get; set; }
    public string? ChangeFreq { get; set; }
    public double? Priority { get; set; }

    // Google News Sitemap extensions (rolling 48 hours)
    public string? Title { get; set; }
    public string? PublicationName { get; set; }
    public string? PublicationLanguage { get; set; } = "en";
    public DateTime? PublishedAt { get; set; }
}

public class SitemapEntriesResponse
{
    public string Type { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 1000;
    public int TotalCount { get; set; }
    public List<SitemapEntryDto> Entries { get; set; } = [];
}

public class BackfillSeoResultDto
{
    public int ArticlesProcessed { get; set; }
    public int SlugsGenerated { get; set; }
    public int ExcerptsGenerated { get; set; }
    public int SourcesProcessed { get; set; }
    public int SourceSlugsGenerated { get; set; }
    public TimeSpan Duration { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class StoryClusterDto
{
    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int ArticleCount { get; set; }
    public int SourceCount { get; set; }
    public List<string> Sources { get; set; } = [];
    public List<StoryClusterArticleDto> Articles { get; set; } = [];
}

public class StoryClusterArticleDto
{
    public string ArticleId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    public string? Url { get; set; }
    public string? ImageUrl { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public float SimilarityScore { get; set; }
}

public class StoryClusterListResponse
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public List<StoryClusterDto> Stories { get; set; } = [];
}

public class TrendingStoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? ImageUrl { get; set; }
    public string Category { get; set; } = "General";
    public int ArticleCount { get; set; }
    public int SourceCount { get; set; }
    public List<string> Sources { get; set; } = [];
    public double TrendingScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<string> Topics { get; set; } = [];
}

public class TrendingStoryListResponse
{
    public int TotalCount { get; set; }
    public List<TrendingStoryDto> Stories { get; set; } = [];
}

// ── Phase 8: Data Pages & State Hubs DTOs ─────────────────────────────────

public class MarketDataDto
{
    public string MetricKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PrimaryValue { get; set; } = string.Empty;
    public string? SecondaryValue { get; set; }
    public string? Change24h { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string SourceAttribution { get; set; } = string.Empty;
    public string SummaryMarkdown { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public List<MarketDataPointDto> HistoricalPoints { get; set; } = [];
    public List<FaqItemDto> Faqs { get; set; } = [];
    public Dictionary<string, FxCurrencyRateDto>? Currencies { get; set; }
}

public class FxCurrencyRateDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public double ParallelBuy { get; set; }
    public double ParallelSell { get; set; }
    public double OfficialRate { get; set; }
    public double? OfficialBuy { get; set; }
    public double? OfficialSell { get; set; }
    public string? Change24h { get; set; }
}

public class MarketDataPointDto
{
    public string Date { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double PrimaryValue { get; set; }
    public double? SecondaryValue { get; set; }
    public string? Note { get; set; }
}

public class StateSummaryDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Capital { get; set; } = string.Empty;
    public string GeopoliticalZone { get; set; } = string.Empty;
    public string? Slogan { get; set; }
    public int ArticleCount { get; set; }
    public List<string> MajorCities { get; set; } = [];
}

public class StateDetailDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Capital { get; set; } = string.Empty;
    public string GeopoliticalZone { get; set; } = string.Empty;
    public string? Slogan { get; set; }
    public string Intro { get; set; } = string.Empty;
    public int ArticleCount { get; set; }
    public List<string> MajorCities { get; set; } = [];
    public List<ArticleDto> Articles { get; set; } = [];
    public List<TopicDto> TopTopics { get; set; } = [];
    public List<FaqItemDto> Faqs { get; set; } = [];
}

public class StateZoneGroupDto
{
    public string Zone { get; set; } = string.Empty;
    public List<StateSummaryDto> States { get; set; } = [];
}

