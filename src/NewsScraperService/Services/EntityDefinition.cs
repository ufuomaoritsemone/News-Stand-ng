namespace NewsScraperService.Services;

public class EntityDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Type { get; set; } = "Theme";
    public string? Description { get; set; }
    public List<string> Aliases { get; set; } = [];
}

public class EntityMatch
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Type { get; set; } = "Theme";
    public string? Description { get; set; }
    public float Confidence { get; set; } = 1.0f;
    public string SourceTier { get; set; } = "Gazetteer";
}
