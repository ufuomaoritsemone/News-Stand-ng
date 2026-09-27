namespace NewsScraperService.Models;

/// <summary>
/// DTO for source records fetched from the NewsApi.
/// Moved from ScraperWorker inner class to shared Models/ folder (Fix #17).
/// </summary>
public class SourceDto
{
    public string Id          { get; set; } = string.Empty;
    public string Name        { get; set; } = string.Empty;
    public string? RssUrl     { get; set; }
    public string? SitemapUrl { get; set; }
    public string ScraperType { get; set; } = "Rss";
}
