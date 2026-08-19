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
    public async Task<IActionResult> GetSources()
    {
        var sources = await _db.Sources.AsNoTracking().ToListAsync();

        if (!sources.Any())
        {
            var defaults = new List<Source>
            {
                new Source { Id = "punch", Name = "Punch Newspaper", RssUrl = "https://punchng.com/feed/", SitemapUrl = "https://punchng.com/news-sitemap.xml", ScraperType = "Hybrid" },
                new Source { Id = "vanguard", Name = "Vanguard", RssUrl = "https://www.vanguardngr.com/feed/", SitemapUrl = "https://www.vanguardngr.com/news-sitemap.xml", ScraperType = "Hybrid" },
                new Source { Id = "premiumtimes", Name = "Premium Times", RssUrl = "https://www.premiumtimesng.com/feed", SitemapUrl = "https://www.premiumtimesng.com/news-sitemap.xml", ScraperType = "Hybrid" },
                new Source { Id = "thecable", Name = "TheCable", RssUrl = "https://www.thecable.ng/feed", SitemapUrl = "https://www.thecable.ng/news-sitemap.xml", ScraperType = "Hybrid" },
                new Source { Id = "dailypost", Name = "Daily Post Nigeria", RssUrl = "https://dailypost.ng/feed/", SitemapUrl = "https://dailypost.ng/news-sitemap.xml", ScraperType = "Hybrid" },
                new Source { Id = "dailytrust", Name = "Daily Trust", RssUrl = "https://dailytrust.com/feed", SitemapUrl = "https://dailytrust.com/news-sitemap.xml", ScraperType = "Hybrid" },
                new Source { Id = "guardian", Name = "The Guardian Nigeria", RssUrl = "https://guardian.ng/feed/", SitemapUrl = "https://guardian.ng/sitemap.xml", ScraperType = "Hybrid" }
            };
            _db.Sources.AddRange(defaults);
            await _db.SaveChangesAsync();
            sources = defaults;
        }

        return Ok(sources);
    }

    [HttpPost]
    public async Task<IActionResult> AddSource([FromBody] SourceInput input)
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
        await _db.SaveChangesAsync();

        int scrapedCount = 0;
        if (!string.IsNullOrWhiteSpace(source.RssUrl))
        {
            try
            {
                var client = _clientFactory.CreateClient();
                scrapedCount = await RssScraperHelper.ScrapeAndSaveFeedAsync(_db, client, source.Name, source.RssUrl);
            }
            catch
            {
                // Background service will re-attempt scraping cycle
            }
        }

        return CreatedAtAction(nameof(GetSources), new { id = source.Id }, new { source.Id, source.Name, source.RssUrl, source.SitemapUrl, source.ScraperType, ScrapedCount = scrapedCount });
    }

    [HttpPost("{id}/scrape")]
    public async Task<IActionResult> ScrapeSource(string id)
    {
        var source = await _db.Sources.FirstOrDefaultAsync(s => s.Id == id);
        if (source == null) return NotFound(new { message = "Source not found." });

        if (string.IsNullOrWhiteSpace(source.RssUrl) && string.IsNullOrWhiteSpace(source.SitemapUrl))
        {
            return BadRequest(new { message = "Source has neither RSS URL nor Sitemap URL configured." });
        }

        int scrapedCount = 0;
        if (!string.IsNullOrWhiteSpace(source.RssUrl))
        {
            var client = _clientFactory.CreateClient();
            scrapedCount = await RssScraperHelper.ScrapeAndSaveFeedAsync(_db, client, source.Name, source.RssUrl);
        }

        return Ok(new { source.Id, source.Name, ScrapedCount = scrapedCount });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSource(string id)
    {
        var source = await _db.Sources.FirstOrDefaultAsync(s => s.Id == id);
        if (source != null)
        {
            _db.Sources.Remove(source);
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }
}

public class SourceInput
{
    public string Name { get; set; } = string.Empty;
    public string? RssUrl { get; set; }
    public string? SitemapUrl { get; set; }
    public string? ScraperType { get; set; } = "Hybrid";
}
