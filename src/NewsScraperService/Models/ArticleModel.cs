namespace NewsScraperService.Models;

public class ArticleModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Url { get; set; }
    public string? Content { get; set; }
    public string? ImageUrl { get; set; }
    public string? Category { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? Source { get; set; }
    public string? AudioUrl { get; set; }
}
