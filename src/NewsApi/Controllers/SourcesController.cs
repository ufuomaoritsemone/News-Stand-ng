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

    private static readonly Dictionary<string, (string Name, string Slug, string Description, string WebsiteUrl)> KnownPublishers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["punch"] = ("Punch Newspaper", "punch", "The Punch is Nigeria's most widely read national daily newspaper, established in 1971 with renowned investigative reporting and extensive nationwide political coverage.", "https://punchng.com"),
            ["vanguard"] = ("Vanguard", "vanguard", "Vanguard Newspapers is a leading Nigerian national daily established in 1984, renowned for authoritative political reportage, security updates, and regional discourse.", "https://www.vanguardngr.com"),
            ["premiumtimes"] = ("Premium Times", "premiumtimes", "Premium Times is a Pulitzer-recognized Nigerian investigative online newspaper established in 2011, specializing in accountability journalism and governance analysis.", "https://www.premiumtimesng.com"),
            ["thecable"] = ("TheCable", "thecable", "TheCable is an independent Nigerian digital newspaper committed to fast-breaking verified news, economic intelligence, and public interest journalism.", "https://www.thecable.ng"),
            ["dailypost"] = ("Daily Post Nigeria", "dailypost", "Daily Post Nigeria is a premier digital news organization delivering breaking news, politics, metro bulletins, and editorial perspectives across Nigeria's 36 states.", "https://dailypost.ng"),
            ["dailytrust"] = ("Daily Trust", "dailytrust", "Daily Trust is an authoritative Nigerian daily newspaper published by Media Trust, renowned for deep coverage across Northern Nigeria and national policy analysis.", "https://dailytrust.com"),
            ["guardian"] = ("The Guardian Nigeria", "guardian", "The Guardian is Nigeria's flagship independent newspaper, established in 1983 with a reputation for intellectual depth, editorial excellence, and cultural commentary.", "https://guardian.ng"),
            ["businessday"] = ("BusinessDay", "businessday", "BusinessDay is West Africa's leading financial and business daily covering macroeconomic trends, energy markets, corporate finance, and technology innovation.", "https://businessday.ng"),
            ["channelstv"] = ("Channels Television", "channelstv", "Channels Television is a multi-award-winning Nigerian independent broadcaster delivering live news packages, investigative dispatches, and primetime television journalism.", "https://www.channelstv.com"),
            ["tvcnews"] = ("TVC News", "tvcnews", "TVC News is a 24-hour Nigerian news channel providing balanced national reportage, current affairs analysis, and live community broadcasts.", "https://tvcnews.tv"),
            ["arisenews"] = ("Arise News", "arisenews", "Arise News is an international news channel broadcasting from London, New York, and Lagos, offering global African perspectives and high-impact political interviews.", "https://www.arise.tv"),
            ["thenation"] = ("The Nation", "thenation", "The Nation is a widely circulated Nigerian daily newspaper founded in 2006, recognized for robust political analysis and community reportage.", "https://thenationonlineng.net"),
            ["leadership"] = ("Leadership Newspaper", "leadership", "Leadership Newspaper is an Abuja-based national daily committed to truth, democracy, and investigative coverage across Nigeria.", "https://leadership.ng")
        };

    [HttpGet]
    public async Task<IActionResult> GetSources(CancellationToken cancellationToken = default)
    {
        var sources = await _db.Sources.AsNoTracking().ToListAsync(cancellationToken);

        // Aggregate counts by publisher name
        var counts = await _db.Articles.AsNoTracking()
            .Where(a => !string.IsNullOrEmpty(a.Source))
            .GroupBy(a => a.Source!.ToLower())
            .Select(g => new { Source = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Source, x => x.Count, cancellationToken);

        if (!sources.Any())
        {
            var defaults = KnownPublishers.Select(kv => new SourceDto
            {
                Id = kv.Key,
                Name = kv.Value.Name,
                Slug = kv.Value.Slug,
                Description = kv.Value.Description,
                WebsiteUrl = kv.Value.WebsiteUrl,
                ScraperType = "Hybrid",
                ArticleCount = counts.GetValueOrDefault(kv.Value.Name.ToLower(), 0)
            }).ToList();
            return Ok(defaults);
        }

        var dtos = sources.Select(s =>
        {
            var slug = !string.IsNullOrEmpty(s.Slug) ? s.Slug : s.Id.ToLowerInvariant();
            var meta = KnownPublishers.TryGetValue(slug, out var kp)
                ? kp
                : KnownPublishers.Values.FirstOrDefault(p => p.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase));

            return new SourceDto
            {
                Id = s.Id,
                Name = s.Name,
                Slug = slug,
                Description = meta != default ? meta.Description : $"Verified publisher reporting across Nigeria via {s.Name}.",
                WebsiteUrl = meta != default ? meta.WebsiteUrl : (s.RssUrl != null ? new Uri(s.RssUrl).GetLeftPart(UriPartial.Authority) : null),
                RssUrl = s.RssUrl,
                SitemapUrl = s.SitemapUrl,
                ScraperType = s.ScraperType,
                ArticleCount = counts.GetValueOrDefault((s.Name ?? string.Empty).ToLower(), 0)
            };
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Returns publisher metadata and editorial profile by slug or ID for source landing pages.
    /// </summary>
    [HttpGet("{slug}")]
    [HttpGet("by-slug/{slug}")]
    public async Task<IActionResult> GetSourceBySlug(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return BadRequest(new { message = "Source slug or identifier is required." });

        var normalized = slug.Trim().ToLowerInvariant();

        var s = await _db.Sources.AsNoTracking()
            .FirstOrDefaultAsync(x => (x.Slug != null && x.Slug.ToLower() == normalized) ||
                                      x.Id.ToLower() == normalized ||
                                      x.Name.ToLower() == normalized, cancellationToken);

        string name = s?.Name ?? string.Empty;
        string? rssUrl = s?.RssUrl;
        string? sitemapUrl = s?.SitemapUrl;
        string scraperType = s?.ScraperType ?? "Hybrid";

        // Check known publishers dictionary for rich bio
        var meta = KnownPublishers.TryGetValue(normalized, out var kp)
            ? kp
            : KnownPublishers.Values.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (s == null && meta == default)
        {
            return NotFound(new { message = $"Publisher source '{slug}' was not found." });
        }

        if (string.IsNullOrEmpty(name) && meta != default)
        {
            name = meta.Name;
        }

        var articleCount = await _db.Articles.AsNoTracking()
            .CountAsync(a => a.Source != null && (a.Source.ToLower() == name.ToLower() || a.Source.ToLower() == normalized), cancellationToken);

        var dto = new SourceDto
        {
            Id = s?.Id ?? normalized,
            Name = name,
            Slug = meta != default ? meta.Slug : normalized,
            Description = meta != default ? meta.Description : $"Authoritative verified reporting published by {name}.",
            WebsiteUrl = meta != default ? meta.WebsiteUrl : (rssUrl != null ? new Uri(rssUrl).GetLeftPart(UriPartial.Authority) : null),
            RssUrl = rssUrl,
            SitemapUrl = sitemapUrl,
            ScraperType = scraperType,
            ArticleCount = articleCount
        };

        return Ok(dto);
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
