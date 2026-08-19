using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;

public class FeedbackItem
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public int Rating { get; set; } = 5;

    [MaxLength(100)]
    public string Category { get; set; } = "General";

    [Required]
    public string Message { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? UserEmail { get; set; }

    [MaxLength(100)]
    public string? UserName { get; set; }

    [MaxLength(50)]
    public string? AppVersion { get; set; }

    [MaxLength(50)]
    public string? Platform { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsEmailSent { get; set; }
}