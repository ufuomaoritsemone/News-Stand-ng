using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;

public class Source
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? RssUrl { get; set; }
    public string? SitemapUrl { get; set; }
    public string? ScraperType { get; set; } = "Hybrid";
}
