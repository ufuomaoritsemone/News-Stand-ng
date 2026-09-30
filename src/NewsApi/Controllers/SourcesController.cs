using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SourcesController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly IHttpClientFactory _clientFactory;

    public SourcesController(NewsDbContext db, IHttpClientFactory clientFactory)
    {
        _db = db;
        _clientFactory = clientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetSources(CancellationToken cancellationToken = default)
    {
        var sources = await _db.Sources.AsNoTracking().ToListAsync(cancellationToken);

        if (!sources.Any())
        {
            var defaults = new List<SourceDto>
            {
                new SourceDto { Id = "punch", Name = "Punch Newspaper", RssUrl = "https://punchng.com/feed/", SitemapUrl = "https://punchng.com/news-sitemap.xml", ScraperType = "Hybrid" },
                new SourceDto { Id = "vanguard", Name = "Vanguard", RssUrl = "https://www.vanguardngr.com/feed/", SitemapUrl = "https://www.vanguardngr.com/news-sitemap.xml", ScraperType = "Hybrid" },
                new SourceDto { Id = "premiumtimes", Name = "Premium Times", RssUrl = "https://www.premiumtimesng.com/feed", SitemapUrl = "https://www.premiumtimesng.com/news-sitemap.xml", ScraperType = "Hybrid" },
                new SourceDto { Id = "thecable", Name = "TheCable", RssUrl = "https://www.thecable.ng/feed", SitemapUrl = "https://www.thecable.ng/news-sitemap.xml", ScraperType = "Hybrid" },
                new SourceDto { Id = "dailypost", Name = "Daily Post Nigeria", RssUrl = "https://dailypost.ng/feed/", SitemapUrl = "https://dailypost.ng/news-sitemap.xml", ScraperType = "Hybrid" },
                new SourceDto { Id = "dailytrust", Name = "Daily Trust", RssUrl = "https://dailytrust.com/feed", SitemapUrl = "https://dailytrust.com/news-sitemap.xml", ScraperType = "Hybrid" },
                new SourceDto { Id = "guardian", Name = "The Guardian Nigeria", RssUrl = "https://guardian.ng/feed/", SitemapUrl = "https://guardian.ng/sitemap.xml", ScraperType = "Hybrid" }
            };
            return Ok(defaults);
        }

        var dtos = sources.Select(s => new SourceDto
        {
            Id = s.Id,
            Name = s.Name,
            RssUrl = s.RssUrl,
            SitemapUrl = s.SitemapUrl,
            ScraperType = s.ScraperType
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost]
    public async Task<IActionResult> AddSource([FromBody] SourceInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return BadRequest(new { message = "Source name is required." });
        }

        var source = new Source
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = input.Name.Trim(),
            RssUrl = input.RssUrl?.Trim(),
            SitemapUrl = input.SitemapUrl?.Trim(),
            ScraperType = string.IsNullOrWhiteSpace(input.ScraperType) ? "Hybrid" : input.ScraperType.Trim()
        };

        _db.Sources.Add(source);
        await _db.SaveChangesAsync(cancellationToken);

        int scrapedCount = 0;
        if (!string.IsNullOrWhiteSpace(source.RssUrl))
        {
            try
            {
                var client = _clientFactory.CreateClient();
                scrapedCount = await RssScraperHelper.ScrapeAndSaveFeedAsync(_db, client, source.Name, source.RssUrl, cancellationToken);
            }
            catch
            {
                // Background service will re-attempt scraping cycle
            }
        }

        return CreatedAtAction(nameof(GetSources), new { id = source.Id }, new { source.Id, source.Name, source.RssUrl, source.SitemapUrl, source.ScraperType, ScrapedCount = scrapedCount });
    }

    [HttpPost("{id}/scrape")]
    public async Task<IActionResult> ScrapeSource(string id, CancellationToken cancellationToken = default)
    {
        var source = await _db.Sources.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (source == null) return NotFound(new { message = "Source not found." });

        if (string.IsNullOrWhiteSpace(source.RssUrl) && string.IsNullOrWhiteSpace(source.SitemapUrl))
        {
            return BadRequest(new { message = "Source has neither RSS URL nor Sitemap URL configured." });
        }

        int scrapedCount = 0;
        if (!string.IsNullOrWhiteSpace(source.RssUrl))
        {
            var client = _clientFactory.CreateClient();
            scrapedCount = await RssScraperHelper.ScrapeAndSaveFeedAsync(_db, client, source.Name, source.RssUrl, cancellationToken);
        }

        return Ok(new { source.Id, source.Name, ScrapedCount = scrapedCount });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSource(string id, CancellationToken cancellationToken = default)
    {
        var source = await _db.Sources.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (source != null)
        {
            _db.Sources.Remove(source);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }
}

public class SourceInput
{
    [Required(ErrorMessage = "Source name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Source name must be between 2 and 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Url(ErrorMessage = "RssUrl must be a valid URL.")]
    [StringLength(500, ErrorMessage = "RssUrl cannot exceed 500 characters.")]
    public string? RssUrl { get; set; }

    [Url(ErrorMessage = "SitemapUrl must be a valid URL.")]
    [StringLength(500, ErrorMessage = "SitemapUrl cannot exceed 500 characters.")]
    public string? SitemapUrl { get; set; }

    [StringLength(50, ErrorMessage = "ScraperType cannot exceed 50 characters.")]
    public string? ScraperType { get; set; } = "Hybrid";
}
