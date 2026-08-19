using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    private readonly ILogger<ArticlesController> _logger;

    public ArticlesController(NewsDbContext db, RelatedContentService relatedService, ILogger<ArticlesController> logger)
    {
        _db = db;
        _relatedService = relatedService;
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
/// <summary> /// Returns articles organized by category for the mobile briefing UI. /// </summary>
[HttpGet("briefings")]
public async Task<IActionResult> GetBriefings([FromQuery] int topPerCategory = 5, [FromQuery] string? language = null)
{
    try
    {
        var preferredLanguage = string.IsNullOrWhiteSpace(language) ? "English" : language;
        _logger.LogInformation("GetBriefings called with topPerCategory={TopPerCategory}, language={Language}", topPerCategory, preferredLanguage);

        var articles = await _db.Articles
            .AsNoTracking()
            .OrderByDescending(a => a.PublishedAt ?? DateTime.MinValue)
            .ToListAsync();

        if (articles.Count == 0)
        {
        _logger.LogWarning("No articles found in database");
            return Ok(new List<BriefingCategoryDto>());}

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

        var latestArticle = articles.FirstOrDefault();
        var lastUpdated = latestArticle?.PublishedAt ?? DateTime.MinValue;
        var hoursStale = (DateTime.UtcNow - lastUpdated).TotalHours;
        Response.Headers.Append("X-Data-Age-Hours", hoursStale.ToString("F1"));

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
        [FromQuery] int pageSize = 50)
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

        var totalCount = await query.CountAsync();

        var audioMap = await _db.AudioAssets.AsNoTracking()
            .ToDictionaryAsync(x => x.ArticleId ?? string.Empty, x => x.Url);

        var effectiveLimit = Math.Min(limit, 500);

        var rawArticles = await query
            .OrderByDescending(a => a.PublishedAt ?? DateTime.MinValue)
            .Take(effectiveLimit)
            .ToListAsync();

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
    public async Task<IActionResult> IngestArticles([FromBody] IngestArticlesRequest request)
    {
        if (request?.Articles == null || !request.Articles.Any())
        {
            return BadRequest(new { message = "No articles provided in payload." });
        }

        int ingested = 0;
        int skipped = 0;

        foreach (var item in request.Articles)
        {
            if (string.IsNullOrWhiteSpace(item.Title)) continue;

            var titleNorm = item.Title.Trim().ToLower();
            var urlNorm = item.Url?.Trim();

            bool exists = await _db.Articles.AnyAsync(a => 
                (!string.IsNullOrEmpty(urlNorm) && a.Url == urlNorm) ||
                a.Title.ToLower() == titleNorm);

            if (exists)
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
            ingested++;
        }

        if (ingested > 0)
        {
            await _db.SaveChangesAsync();
        }

        _logger.LogInformation("Ingested {IngestedCount} new articles, skipped {SkippedCount} duplicates", ingested, skipped);

        return Ok(new IngestArticlesResponse
        {
            IngestedCount = ingested,
            SkippedDuplicateCount = skipped
        });
    }

    [HttpPut("{id}/category")]
    public async Task<IActionResult> UpdateCategory(string id, [FromBody] UpdateArticleCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Category))
        {
            return BadRequest(new { message = "Category is required." });
        }

        var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == id);
        if (article == null)
        {
            return NotFound(new { message = $"Article with ID '{id}' was not found." });
        }

        string oldCategory = article.Category ?? "General";
        article.Category = request.Category.Trim();
        await _db.SaveChangesAsync();

        await SaveCorrectionFeedbackAsync(article, oldCategory);

        _logger.LogInformation("Updated category for article '{ArticleId}' from '{OldCategory}' to '{NewCategory}'", id, oldCategory, article.Category);

        return Ok(new { id = article.Id, category = article.Category, message = "Category updated successfully and saved to training dataset feedback." });
    }

    private async Task SaveCorrectionFeedbackAsync(Article article, string oldCategory)
    {
        try
        {
            var feedbackRecord = new
            {
                Title = article.Title,
                Summary = article.Summary ?? "",
                Category = article.Category,
                OldCategory = oldCategory,
                Source = article.Source ?? "AdminFeedback",
                Url = article.Url ?? "",
                UpdatedAt = DateTime.UtcNow
            };

            var paths = new List<string>
            {
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/NewsCategorizer.Trainer/corrected_dataset.json")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/NewsCategorizer.Trainer/corrected_dataset.json")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "corrected_dataset.json"))
            };

            foreach (var path in paths)
            {
                try
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        var records = new List<object>();
                        if (System.IO.File.Exists(path))
                        {
                            var jsonText = await System.IO.File.ReadAllTextAsync(path);
                            var existing = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(jsonText);
                            if (existing != null)
                            {
                                foreach (var el in existing) records.Add(el);
                            }
                        }
                        records.Add(feedbackRecord);
                        var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                        await System.IO.File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(records, opts));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not write correction feedback to path {Path}: {Message}", path, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save correction feedback record.");
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

