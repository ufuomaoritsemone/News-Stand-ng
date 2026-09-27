namespace NewsApi.Models;

/// <summary> /// DTO for briefing items sent to mobile clients. /// </summary> 
public class BriefingItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Content { get; set; }
    public string? Url { get; set; }
    public string? Source { get; set; }
    public string? Category { get; set; }
    public DateTime PublishedAt { get; set; }
    public string? ImageUrl { get; set; }
    public string? AudioUrl { get; set; }
    public string? Language { get; set; }
    public string? VoiceName { get; set; }
    public string? Author { get; set; }
    public string? ContentType { get; set; } = "News";

    // Direct In-House Sponsorship Engine Fields
    public bool IsSponsored { get; set; }
    public string? SponsorName { get; set; }
    public string? SponsorUrl { get; set; }
    public bool IsPinned { get; set; }
    public int? TargetPosition { get; set; }
    public int PriorityWeight { get; set; } = 1;
}

// <summary> /// DTO for briefing categories. /// </summary>
public class BriefingCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public List<BriefingItemDto> Top { get; set; } = new();
}