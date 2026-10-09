namespace NewsApi.Models;

/// <summary>
/// Represents a dynamic, database-persisted system configuration setting.
/// Overrides appsettings.json and environment variables at runtime without service restarts.
/// </summary>
public class SystemSetting
{
    /// <summary>
    /// Configuration key identifier (e.g. "Gemini:ApiKey", "YouTube:ApiKey", "Feedback:Smtp:Host").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The configured value.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Functional grouping category (e.g. "AI", "Media", "Audio", "SEO", "Email", "Scraper", "Security").
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// Human-readable description or label for the setting.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates whether the value contains sensitive credentials (API key, password) that should be masked in UI responses.
    /// </summary>
    public bool IsSecret { get; set; } = false;

    /// <summary>
    /// Timestamp of last modification.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public record SystemSettingItemDto(
    string Key,
    string? Value,
    string Category,
    string? Description,
    bool IsSecret,
    bool IsConfigured,
    DateTimeOffset UpdatedAt
);

public record SystemDiagnosticsDto(
    string DbProvider,
    string DbStatus,
    int TotalArticles,
    int TotalSources,
    int TotalFeedbacks,
    int TotalComments,
    string EnvironmentName,
    DateTimeOffset ServerTimeUtc,
    string AppVersion
);

public record SystemSettingsResponseDto(
    SystemDiagnosticsDto Diagnostics,
    List<SystemSettingItemDto> Settings
);

public record UpdateSettingsRequestDto(
    Dictionary<string, string?> Settings
);

public record TestConnectionRequestDto(
    string? ApiKey,
    string? Target
);

public record TestConnectionResponseDto(
    bool Success,
    string Message,
    int? StatusCode = null
);
