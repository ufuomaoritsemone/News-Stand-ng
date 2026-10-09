using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Models;
using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/states")]
public class StatesController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly ILogger<StatesController> _logger;

    public StatesController(NewsDbContext db, ILogger<StatesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Returns all 36 states + FCT grouped by geopolitical zone with article counts.
    /// </summary>
    [HttpGet]
    [ResponseCache(Duration = 600)]
    public async Task<ActionResult<List<StateZoneGroupDto>>> GetStates()
    {
        var allStates = NigerianStateRegistry.AllStates;

        // Query topic counts for states to enrich article counts
        var stateNames = allStates.Select(s => s.Name).ToList();
        var rawTopics = await _db.Topics
            .AsNoTracking()
            .Where(t => t.Type == TopicType.Place || stateNames.Contains(t.Name))
            .Select(t => new { Name = t.Name.ToLower(), Count = t.ArticleTopics.Count })
            .ToListAsync();

        var topicCounts = rawTopics
            .GroupBy(t => t.Name)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        var dtos = allStates.Select(s =>
        {
            var key = s.Name.ToLowerInvariant();
            var count = topicCounts.TryGetValue(key, out var c) ? c : 0;
            if (count == 0 && topicCounts.TryGetValue($"{key} state", out var c2))
            {
                count = c2;
            }

            return new StateSummaryDto
            {
                Name = s.Name,
                Slug = s.Slug,
                Capital = s.Capital,
                GeopoliticalZone = s.GeopoliticalZone,
                Slogan = s.Slogan,
                ArticleCount = count,
                MajorCities = s.MajorCities.ToList()
            };
        }).ToList();

        var grouped = dtos
            .GroupBy(s => s.GeopoliticalZone)
            .OrderBy(g => ZoneOrder(g.Key))
            .Select(g => new StateZoneGroupDto
            {
                Zone = g.Key,
                States = g.OrderBy(s => s.Name).ToList()
            })
            .ToList();

        return Ok(grouped);
    }

    /// <summary>
    /// Returns state landing hub details, latest localized news articles, top topics, and FAQ schema.
    /// </summary>
    [HttpGet("{slug}")]
    [ResponseCache(Duration = 180)]
    public async Task<ActionResult<StateDetailDto>> GetStateBySlug(string slug, [FromQuery] int pageSize = 18)
    {
        var stateInfo = NigerianStateRegistry.FindBySlug(slug);
        if (stateInfo == null)
        {
            return NotFound(new { message = $"State '{slug}' not found." });
        }

        pageSize = Math.Clamp(pageSize, 5, 50);

        // Find topics related to this state, its capital, and major cities
        var candidateNames = new List<string> { stateInfo.Name, $"{stateInfo.Name} State", stateInfo.Capital };
        candidateNames.AddRange(stateInfo.MajorCities);

        var matchingTopicIds = await _db.Topics
            .AsNoTracking()
            .Where(t => candidateNames.Contains(t.Name))
            .Select(t => t.Id)
            .ToListAsync();

        IQueryable<Article> query;

        if (matchingTopicIds.Count > 0)
        {
            // Articles linked via ArticleTopics
            query = _db.Articles
                .AsNoTracking()
                .Where(a => a.ArticleTopics.Any(at => matchingTopicIds.Contains(at.TopicId)));
        }
        else
        {
            // Fallback: search by state name or capital in title or summary
            var stateTerm = stateInfo.Name.ToLower();
            var capitalTerm = stateInfo.Capital.ToLower();

            query = _db.Articles
                .AsNoTracking()
                .Where(a => a.Title.ToLower().Contains(stateTerm) ||
                            (a.Summary != null && a.Summary.ToLower().Contains(stateTerm)) ||
                            a.Title.ToLower().Contains(capitalTerm));
        }

        var totalCount = await query.CountAsync();

        var rawArticles = await query
            .OrderByDescending(a => a.PublishedAt)
            .Take(pageSize)
            .ToListAsync();

        var articles = rawArticles.Select(a => new ArticleDto
        {
            Id = a.Id,
            Title = a.Title,
            Slug = a.Slug,
            Summary = a.Summary,
            Excerpt = a.Excerpt,
            Source = a.Source ?? string.Empty,
            Category = a.Category ?? string.Empty,
            PublishedAt = a.PublishedAt,
            ImageUrl = CategoryImageMap.Resolve(a.ImageUrl, a.Category),
            IsSponsored = a.IsSponsored,
            IsPinned = a.IsPinned
        }).ToList();

        // Top topics associated with this state
        var topTopics = await _db.Topics
            .AsNoTracking()
            .Where(t => candidateNames.Contains(t.Name) || t.ArticleTopics.Any(at => query.Select(a => a.Id).Take(50).Contains(at.ArticleId)))
            .OrderByDescending(t => t.ArticleTopics.Count)
            .Take(6)
            .Select(t => new TopicDto
            {
                Id = t.Id,
                Slug = t.Slug,
                Name = t.Name,
                Type = t.Type.ToString(),
                Description = t.Description,
                ArticleCount = t.ArticleTopics.Count,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        var faqs = new List<FaqItemDto>
        {
            new($"What is the administrative capital of {stateInfo.Name}?",
                $"{stateInfo.Capital} is the official capital city of {stateInfo.Name}, serving as the administrative seat of the state government."),
            new($"Which geopolitical zone does {stateInfo.Name} belong to?",
                $"{stateInfo.Name} is located in the {stateInfo.GeopoliticalZone} geopolitical zone of the Federal Republic of Nigeria."),
            new($"What are the major commercial and metropolitan cities in {stateInfo.Name}?",
                $"The major urban, commercial, and administrative centers in {stateInfo.Name} include {string.Join(", ", stateInfo.MajorCities)}."),
            new($"How often is local news for {stateInfo.Name} updated on News Stand NG?",
                $"News Stand NG aggregates verified news coverage for {stateInfo.Name} in real time from leading national and regional Nigerian newsrooms, updating as breaking stories develop.")
        };

        var detail = new StateDetailDto
        {
            Name = stateInfo.Name,
            Slug = stateInfo.Slug,
            Capital = stateInfo.Capital,
            GeopoliticalZone = stateInfo.GeopoliticalZone,
            Slogan = stateInfo.Slogan,
            Intro = stateInfo.Intro,
            ArticleCount = totalCount,
            MajorCities = stateInfo.MajorCities.ToList(),
            Articles = articles,
            TopTopics = topTopics,
            Faqs = faqs
        };

        return Ok(detail);
    }

    private static int ZoneOrder(string zone) => zone switch
    {
        "South West" => 1,
        "South South" => 2,
        "South East" => 3,
        "North Central" => 4,
        "North West" => 5,
        "North East" => 6,
        _ => 99
    };
}
