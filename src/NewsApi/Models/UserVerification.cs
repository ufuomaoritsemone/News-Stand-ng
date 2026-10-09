namespace NewsApi.Models;

public class UserVerification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Identifier { get; set; } = string.Empty; // Email or Google Subject ID
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Channel { get; set; } = "Email"; // "Email" or "Google"
    public string? OtpHash { get; set; }
    public int AttemptCount { get; set; } = 0;
    public bool IsVerified { get; set; } = false;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
