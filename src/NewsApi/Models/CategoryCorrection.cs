namespace NewsApi.Models;

public class CategoryCorrection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ArticleId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string OldCategory { get; set; } = string.Empty;
    public string NewCategory { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? Url { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
