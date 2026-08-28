using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ArticlesController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly RelatedContentService _relatedService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ArticlesController> _logger;

    private const string BriefingsCachePrefix = "briefings_";

    public ArticlesController(
        NewsDbContext db, 
        RelatedContentService relatedService, 
        IMemoryCache cache,
        ILogger<ArticlesController> logger)
    {
        _db = db;
        _relatedService = relatedService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Returns related archive articles, video stories, and tweets based on keyword/entity and category matching.
    /// </summary>
    [HttpGet("related")]
    public async Task<IActionResult> GetRelatedStories(
        [FromQuery] string? id = null,
        [FromQuery] string? title = null,
        [FromQuery] string? category = null,
        [FromQuery] int limit = 4,
        CancellationToken cancellationToken = default)
    {
        var effectiveLimit = Math.Clamp(limit, 1, 20);
        var result = await _relatedService.GetRelatedStoriesAsync(id, title, category, effectiveLimit, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns articles organized by category for the mobile briefing UI with in-memory caching and freshness windowing.
    /// </summary>
    [HttpGet("briefings")]
    public async Task<IActionResult> GetBriefings(
        [FromQuery] int topPerCategory = 5, 
        [FromQuery] string? language = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var preferredLanguage = string.IsNullOrWhiteSpace(language) ? "English" : language.Trim();
            var cacheKey = $"{BriefingsCachePrefix}{preferredLanguage}_{topPerCategory}";

            if (_cache.TryGetValue(cacheKey, out List<BriefingCategoryDto>? cachedBriefings) && cachedBriefings != null)
            {
                Response.Headers.Append("X-Cache", "HIT");
                return Ok(cachedBriefings);
            }

            _logger.LogInformation("GetBriefings cache miss. Building briefing with topPerCategory={TopPerCategory}, language={Language}", topPerCategory, preferredLanguage);

            // Freshness cutoff (last 72 hours) to avoid scanning full table into memory
            var cutoff = DateTime.UtcNow.AddDays(-3);
            var articles = await _db.Articles
                .AsNoTracking()
                .Where(a => a.PublishedAt >= cutoff)
                .OrderByDescending(a => a.PublishedAt)
                .ToListAsync(cancellationToken);

            // Fallback to top recent articles if recent window is sparse
            if (articles.Count == 0)
            {
                articles = await _db.Articles
                    .AsNoTracking()
                    .OrderByDescending(a => a.PublishedAt ?? DateTime.MinValue)
                    .Take(200)
                    .ToListAsync(cancellationToken);
            }

            if (articles.Count == 0)
            {
                _logger.LogWarning("No articles found in database");
                return Ok(new List<BriefingCategoryDto>());
            }

            var briefing = articles
                .GroupBy(a => string.IsNullOrWhiteSpace(a.Category) ? "General" : a.Category)
                .Select(g => new BriefingCategoryDto
                {
                    Category = g.Key,
                    Top = g.Take(topPerCategory)
                        .Select(a => new BriefingItemDto
                        {
                            Id = a.Id,
                            Title = a.Title,
                            Summary = a.Summary,
                            Content = a.Content,
                            Url = a.Url,
                            ImageUrl = GetValidImageUrl(a.ImageUrl, a.Category, a.Source),
                            Source = a.Source ?? "General News",
                            Category = a.Category ?? "General",
                            PublishedAt = a.PublishedAt ?? DateTime.UtcNow,
                            AudioUrl = a.AudioUrl,
                            Language = preferredLanguage,
                            VoiceName = preferredLanguage switch
                            {
                                "Yoruba" => "Yoruba Female",
                                "Igbo" => "Igbo Female",
                                "Hausa" => "Hausa Female",
                                _ => "English Female"
                            }
                        }).ToList()
                })
                .ToList();

            // Cache for 3 minutes sliding, 5 minutes absolute
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(3))
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

            _cache.Set(cacheKey, briefing, cacheOptions);

            var latestArticle = articles.FirstOrDefault();
            var lastUpdated = latestArticle?.PublishedAt ?? DateTime.MinValue;
            var hoursStale = (DateTime.UtcNow - lastUpdated).TotalHours;
            Response.Headers.Append("X-Data-Age-Hours", hoursStale.ToString("F1"));
            Response.Headers.Append("X-Cache", "MISS");

            return Ok(briefing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving briefings");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetArticles(
        [FromQuery] string? source, 
        [FromQuery] string? category, 
        [FromQuery] string? search, 
        [FromQuery] bool todayOnly = false, 
        [FromQuery] int limit = 200,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Articles.AsNoTracking();

        if (todayOnly)
        {
            var today = DateTime.UtcNow.Date;
            query = query.Where(a => a.PublishedAt != null && a.PublishedAt >= today);
        }

        if (!string.IsNullOrWhiteSpace(source) && !source.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var s = source.Trim().ToLower();
            query = query.Where(a => a.Source != null && a.Source.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var c = category.Trim().ToLower();
            query = query.Where(a => a.Category != null && a.Category.ToLower() == c);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(a => a.Title.ToLower().Contains(term) || 
                                     (a.Summary != null && a.Summary.ToLower().Contains(term)) ||
                                     (a.Content != null && a.Content.ToLower().Contains(term)));
        }

        var pageIndex = Math.Max(1, page);
        var size = Math.Clamp(pageSize > 0 ? pageSize : limit, 1, 200);

        var audioMap = await _db.AudioAssets.AsNoTracking()
            .ToDictionaryAsync(x => x.ArticleId ?? string.Empty, x => x.Url, cancellationToken);

        var rawArticles = await query
            .OrderByDescending(a => a.PublishedAt ?? DateTime.MinValue)
            .Skip((pageIndex - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        var articleDtos = rawArticles.Select(a => new ArticleDto
        {
            Id = a.Id,
            Title = a.Title,
            Summary = a.Summary,
            Content = a.Content,
            Url = a.Url,
            ImageUrl = GetValidImageUrl(a.ImageUrl, a.Category, a.Source),
            Source = string.IsNullOrWhiteSpace(a.Source) ? "General News" : a.Source,
            Category = string.IsNullOrWhiteSpace(a.Category) ? "General" : a.Category,
            PublishedAt = a.PublishedAt ?? DateTime.UtcNow,
            AudioUrl = audioMap.ContainsKey(a.Id) ? audioMap[a.Id] : a.AudioUrl
        }).ToList();

        return Ok(articleDtos);
    }

    [HttpPost("ingest")]
    public async Task<IActionResult> IngestArticles([FromBody] IngestArticlesRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Articles == null || !request.Articles.Any())
        {
            return BadRequest(new { message = "No articles provided in payload." });
        }

        var incomingList = request.Articles
            .Where(i => !string.IsNullOrWhiteSpace(i.Title))
            .ToList();

        if (!incomingList.Any())
        {
            return Ok(new IngestArticlesResponse { IngestedCount = 0, SkippedDuplicateCount = 0 });
        }

        // Batch duplicate check using HashSet for O(1) in-memory lookups
        var incomingUrls = incomingList
            .Select(a => a.Url?.Trim())
            .Where(u => !string.IsNullOrEmpty(u))
            .ToList();

        var existingUrls = (await _db.Articles
            .AsNoTracking()
            .Where(a => incomingUrls.Contains(a.Url))
            .Select(a => a.Url)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var incomingTitles = incomingList
            .Select(a => a.Title.Trim().ToLower())
            .ToList();

        var existingTitles = (await _db.Articles
            .AsNoTracking()
            .Where(a => incomingTitles.Contains(a.Title.ToLower()))
            .Select(a => a.Title.ToLower())
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int ingested = 0;
        int skipped = 0;

        foreach (var item in incomingList)
        {
            var titleNorm = item.Title.Trim().ToLower();
            var urlNorm = item.Url?.Trim();

            bool isDuplicate = (!string.IsNullOrEmpty(urlNorm) && existingUrls.Contains(urlNorm)) ||
                               existingTitles.Contains(titleNorm);

            if (isDuplicate)
            {
                skipped++;
                continue;
            }

            var entity = new Article
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = item.Title.Trim(),
                Summary = item.Summary,
                Content = item.Content,
                Url = item.Url,
                ImageUrl = item.ImageUrl,
                Source = item.Source ?? "Scraped News",
                Category = item.Category ?? "General",
                PublishedAt = item.PublishedAt ?? DateTime.UtcNow,
                AudioUrl = item.AudioUrl
            };

            _db.Articles.Add(entity);
            if (!string.IsNullOrEmpty(urlNorm)) existingUrls.Add(urlNorm);
            existingTitles.Add(titleNorm);
            ingested++;
        }

        if (ingested > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            // Invalidate briefings cache when new stories arrive
            ClearBriefingCache();
        }

        _logger.LogInformation("Ingested {IngestedCount} new articles, skipped {SkippedCount} duplicates", ingested, skipped);

        return Ok(new IngestArticlesResponse
        {
            IngestedCount = ingested,
            SkippedDuplicateCount = skipped
        });
    }

    [HttpPut("{id}/category")]
    public async Task<IActionResult> UpdateCategory(string id, [FromBody] UpdateArticleCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Category))
        {
            return BadRequest(new { message = "Category is required." });
        }

        var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article == null)
        {
            return NotFound(new { message = $"Article with ID '{id}' was not found." });
        }

        string oldCategory = article.Category ?? "General";
        article.Category = request.Category.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        await SaveCorrectionFeedbackAsync(article, oldCategory, cancellationToken);

        ClearBriefingCache();

        _logger.LogInformation("Updated category for article '{ArticleId}' from '{OldCategory}' to '{NewCategory}'", id, oldCategory, article.Category);

        return Ok(new { id = article.Id, category = article.Category, message = "Category updated successfully and saved to training dataset feedback." });
    }

    private void ClearBriefingCache()
    {
        // Simple cache invalidation for known language combinations
        string[] languages = ["English", "Yoruba", "Igbo", "Hausa"];
        int[] topLimits = [3, 5, 10, 15, 20];
        foreach (var lang in languages)
        {
            foreach (var top in topLimits)
            {
                _cache.Remove($"{BriefingsCachePrefix}{lang}_{top}");
            }
        }
    }

    private async Task SaveCorrectionFeedbackAsync(Article article, string oldCategory, CancellationToken cancellationToken)
    {
        try
        {
            var correction = new CategoryCorrection
            {
                ArticleId = article.Id,
                Title = article.Title,
                Summary = article.Summary,
                OldCategory = oldCategory,
                NewCategory = article.Category ?? "General",
                Source = article.Source,
                Url = article.Url,
                CreatedAt = DateTime.UtcNow
            };

            _db.CategoryCorrections.Add(correction);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Persisted category correction to database for article {ArticleId}", article.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save category correction entity to database.");
        }
    }

    private static string GetValidImageUrl(string? existingUrl, string? category, string? source)
    {
        if (!string.IsNullOrWhiteSpace(existingUrl) && (existingUrl.StartsWith("http://") || existingUrl.StartsWith("https://")))
        {
            return existingUrl;
        }

        category = category?.ToLowerInvariant() ?? "";
        if (category.Contains("politic")) return "https://images.unsplash.com/photo-1540910419892-4a36d2c3266c?w=600&auto=format&fit=crop&q=80";
        if (category.Contains("business") || category.Contains("econom")) return "https://images.unsplash.com/photo-1611974789855-9c2a0a7236a3?w=600&auto=format&fit=crop&q=80";
        if (category.Contains("sport")) return "https://images.unsplash.com/photo-1508098682722-e99c43a406b2?w=600&auto=format&fit=crop&q=80";
        if (category.Contains("tech")) return "https://images.unsplash.com/photo-1518770660439-4636190af475?w=600&auto=format&fit=crop&q=80";
        if (category.Contains("entertain")) return "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=600&auto=format&fit=crop&q=80";

        return "https://images.unsplash.com/photo-1585829365295-ab7cd400c167?w=600&auto=format&fit=crop&q=80";
    }
}

public class UpdateArticleCategoryRequest
{
    public string Category { get; set; } = string.Empty;
}
