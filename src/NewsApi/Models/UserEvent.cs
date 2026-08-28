using System.ComponentModel.DataAnnotations;

namespace NewsApi.Models;

/// <summary>
/// Represents a single anonymised user behaviour event sent from the mobile app.
/// Events are keyed to a random DeviceId (no PII); they are cleaned up
/// once received (the client flushes its local queue on success).
/// </summary>
public class UserEvent
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Random UUID generated on first app launch; stored in MAUI Preferences.</summary>
    [MaxLength(64)]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>One of: app_open, article_read, article_share, article_bookmark</summary>
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>Article ID, if applicable.</summary>
    [MaxLength(64)]
    public string? ArticleId { get; set; }

    /// <summary>Article title snapshot for readability in the dashboard.</summary>
    [MaxLength(300)]
    public string? ArticleTitle { get; set; }

    /// <summary>Article category at time of event.</summary>
    [MaxLength(100)]
    public string? Category { get; set; }

    /// <summary>e.g. Android, iOS, WinUI</summary>
    [MaxLength(50)]
    public string? Platform { get; set; }

    /// <summary>App version string from the client.</summary>
    [MaxLength(30)]
    public string? AppVersion { get; set; }

    /// <summary>UTC timestamp when the event occurred on the device.</summary>
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp when the server received the event.</summary>
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}
