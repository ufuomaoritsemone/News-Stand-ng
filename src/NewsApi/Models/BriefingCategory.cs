namespace NigerianNewsGrid.Client.Models;

public class BriefingCategory
{
    public string Category { get; set; } = string.Empty;
    public List<BriefingItem> Top { get; set; } = new();
}