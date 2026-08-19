using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;

public class Briefing
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Category { get; set; } = "General";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    // JSON payload of top articles (store small snapshot)
    public string PayloadJson { get; set; } = string.Empty;
}
