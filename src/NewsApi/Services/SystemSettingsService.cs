using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NewsApi.Data;
using NewsApi.Models;

namespace NewsApi.Services;

public class SystemSettingsService(
    NewsDbContext db,
    IConfiguration configuration,
    IHostEnvironment environment,
    IMemoryCache cache,
    ILogger<SystemSettingsService> logger) : ISystemSettingsService
{
    private const string CachePrefix = "SysSetting_";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    // Metadata definitions for all standard known settings
    private static readonly Dictionary<string, (string Category, string Description, bool IsSecret, string? Default)> KnownSettings = new(StringComparer.OrdinalIgnoreCase)
    {
        // AI & Machine Learning
        ["Gemini:ApiKey"] = ("AI", "Google Gemini API Key for semantic story clustering and automated SEO", true, null),
        ["Gemini:Model"] = ("AI", "Google Gemini generative model identifier (e.g. gemini-3.1-flash-lite)", false, "gemini-3.1-flash-lite"),
        ["AiEnrichment:GeminiEnabled"] = ("AI", "Enable automated generative AI summary and entity enrichment", false, "true"),
        ["LlmEntityExtraction:Enabled"] = ("AI", "Enable Tier-2 LLM named-entity extraction during RSS scraping", false, "false"),

        // Media & Syndication
        ["YouTube:ApiKey"] = ("Media", "YouTube Data API v3 Key for Nigerian news video channels and trending sync", true, null),
        ["MediaSync:IntervalMinutes"] = ("Media", "Background interval in minutes between YouTube video sync passes", false, "15"),

        // Voice & Audio
        ["GoogleCloud:ApiKey"] = ("Audio", "Google Cloud Text-to-Speech API key for Neural2 en-NG voices (Optional; fallback is Edge Neural)", true, null),
        ["Tts:Provider"] = ("Audio", "Preferred TTS synthesis engine ('EdgeNeural' or 'GoogleCloud')", false, "EdgeNeural"),

        // SEO & Indexing
        ["IndexNow:Key"] = ("SEO", "IndexNow protocol API key for instant Bing/Yandex search indexing", false, "3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d"),
        ["IndexNow:Host"] = ("SEO", "Canonical public host for IndexNow submission (e.g. newsstand.ng)", false, "newsstand.ng"),
        ["IndexNow:Enabled"] = ("SEO", "Enable automated search engine notification on new article publication", false, "true"),
        ["Google:ClientId"] = ("Security", "Google OAuth 2.0 Web Client ID for reader authentication and comments", false, "434931027697-0ofh3nptkv409vju55od3fhrfbkccu15.apps.googleusercontent.com"),

        // Email & Notifications
        ["Feedback:RecipientEmail"] = ("Email", "Email address that receives user feedback alerts", false, "feedback@nigeriannewsgrid.com"),
        ["Feedback:Smtp:Host"] = ("Email", "SMTP outgoing server host (e.g. smtp.mailgun.org or smtp.gmail.com)", false, null),
        ["Feedback:Smtp:Port"] = ("Email", "SMTP outgoing port (usually 587 for STARTTLS or 465 for SSL)", false, "587"),
        ["Feedback:Smtp:EnableSsl"] = ("Email", "Enable SSL/TLS encryption for outgoing SMTP emails", false, "true"),
        ["Feedback:Smtp:UserName"] = ("Email", "SMTP server authentication username / API key", false, null),
        ["Feedback:Smtp:Password"] = ("Email", "SMTP server authentication password / API key secret", true, null),
        ["Feedback:Smtp:FromEmail"] = ("Email", "Sender email address displayed on outgoing emails", false, "no-reply@nigeriannewsgrid.com"),
        ["Feedback:Smtp:FromName"] = ("Email", "Sender display name (e.g. 'News Stand NG')", false, "News Stand NG"),

        // Scraper & Background Workers
        ["Scraper:IntervalMinutes"] = ("Scraper", "Frequency in minutes between automated RSS news scraping cycles", false, "15"),

        // Security & Rate Limiting
        ["RateLimiting:PermitLimit"] = ("Security", "Sliding window rate limit (max requests per minute per remote IP)", false, "120"),
    };

    public async Task<string?> GetSettingAsync(string key, string? defaultValue = null, CancellationToken ct = default)
    {
        var cacheKey = CachePrefix + key;
        if (cache.TryGetValue(cacheKey, out string? cachedVal))
        {
            return cachedVal ?? defaultValue;
        }

        string? resolvedValue = null;

        // 1. Try Database override
        try
        {
            var dbSetting = await db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);
            if (dbSetting != null && !string.IsNullOrWhiteSpace(dbSetting.Value))
            {
                resolvedValue = dbSetting.Value;
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not fetch dynamic setting {Key} from database; checking configuration", key);
        }

        // 2. Fall back to configuration file / environment variable
        if (string.IsNullOrWhiteSpace(resolvedValue))
        {
            resolvedValue = configuration[key];
            if (string.IsNullOrWhiteSpace(resolvedValue))
            {
                // Check environment variable variations (e.g. GEMINI_API_KEY, YOUTUBE_API_KEY)
                var envKey = key.Replace(":", "__");
                resolvedValue = configuration[envKey] ?? Environment.GetEnvironmentVariable(envKey);

                if (string.IsNullOrWhiteSpace(resolvedValue) && key.Equals("Gemini:ApiKey", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedValue = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
                }
                else if (string.IsNullOrWhiteSpace(resolvedValue) && key.Equals("Gemini:Model", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedValue = Environment.GetEnvironmentVariable("GEMINI_MODEL");
                }
                else if (string.IsNullOrWhiteSpace(resolvedValue) && key.Equals("YouTube:ApiKey", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedValue = Environment.GetEnvironmentVariable("YOUTUBE_API_KEY");
                }
                else if (string.IsNullOrWhiteSpace(resolvedValue) && key.Equals("GoogleCloud:ApiKey", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedValue = Environment.GetEnvironmentVariable("GCP_TTS_API_KEY");
                }
                else if (string.IsNullOrWhiteSpace(resolvedValue) && key.Equals("IndexNow:Key", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedValue = Environment.GetEnvironmentVariable("INDEXNOW_KEY");
                }
            }
        }

        // 3. Fall back to known default
        if (string.IsNullOrWhiteSpace(resolvedValue) && KnownSettings.TryGetValue(key, out var meta) && !string.IsNullOrWhiteSpace(meta.Default))
        {
            resolvedValue = meta.Default;
        }

        resolvedValue ??= defaultValue;

        cache.Set(cacheKey, resolvedValue, CacheDuration);
        return resolvedValue;
    }

    public async Task<bool> GetBoolSettingAsync(string key, bool defaultValue = false, CancellationToken ct = default)
    {
        var str = await GetSettingAsync(key, defaultValue.ToString(), ct);
        return bool.TryParse(str, out var result) ? result : defaultValue;
    }

    public async Task<int> GetIntSettingAsync(string key, int defaultValue = 0, CancellationToken ct = default)
    {
        var str = await GetSettingAsync(key, defaultValue.ToString(), ct);
        return int.TryParse(str, out var result) ? result : defaultValue;
    }

    public async Task SetSettingAsync(string key, string? value, string category = "General", string? description = null, bool isSecret = false, CancellationToken ct = default)
    {
        var existing = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing != null)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(category)) existing.Category = category;
            if (!string.IsNullOrWhiteSpace(description)) existing.Description = description;
            existing.IsSecret = isSecret;
        }
        else
        {
            db.SystemSettings.Add(new SystemSetting
            {
                Key = key,
                Value = value,
                Category = category,
                Description = description,
                IsSecret = isSecret,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
        InvalidateCache();
        logger.LogInformation("System setting {Key} successfully updated in database.", key);
    }

    public async Task UpdateSettingsBatchAsync(Dictionary<string, string?> updates, CancellationToken ct = default)
    {
        if (updates == null || updates.Count == 0) return;

        var keys = updates.Keys.ToList();
        var existingRecords = await db.SystemSettings.Where(s => keys.Contains(s.Key)).ToListAsync(ct);
        var existingDict = existingRecords.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in updates)
        {
            // Ignore masked password indicators (e.g. if the user didn't change a secret field)
            if (!string.IsNullOrEmpty(value) && (value.StartsWith("•••") || value.StartsWith("***")))
            {
                continue;
            }

            KnownSettings.TryGetValue(key, out var meta);
            var category = meta.Category ?? "General";
            var description = meta.Description;
            var isSecret = meta.IsSecret;

            if (existingDict.TryGetValue(key, out var record))
            {
                record.Value = value;
                record.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                db.SystemSettings.Add(new SystemSetting
                {
                    Key = key,
                    Value = value,
                    Category = category,
                    Description = description,
                    IsSecret = isSecret,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        await db.SaveChangesAsync(ct);
        InvalidateCache();
        logger.LogInformation("Batch updated {Count} system settings in database.", updates.Count);
    }

    public async Task<SystemSettingsResponseDto> GetSettingsWithDiagnosticsAsync(CancellationToken ct = default)
    {
        // 1. Gather diagnostics
        var isPostgres = db.Database.ProviderName?.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true;
        var provider = isPostgres ? "PostgreSQL" : "SQLite";

        string dbStatus = "Connected (Healthy)";
        try
        {
            await db.Database.CanConnectAsync(ct);
        }
        catch (Exception ex)
        {
            dbStatus = $"Degraded: {ex.Message}";
        }

        var totalArticles = await db.Articles.CountAsync(ct);
        var totalSources = await db.Sources.CountAsync(ct);
        var totalFeedbacks = await db.Feedbacks.CountAsync(ct);
        var totalComments = await db.StoryComments.CountAsync(ct);

        var diagnostics = new SystemDiagnosticsDto(
            DbProvider: provider,
            DbStatus: dbStatus,
            TotalArticles: totalArticles,
            TotalSources: totalSources,
            TotalFeedbacks: totalFeedbacks,
            TotalComments: totalComments,
            EnvironmentName: environment.EnvironmentName,
            ServerTimeUtc: DateTimeOffset.UtcNow,
            AppVersion: "1.0.0"
        );

        // 2. Compile current settings
        var dbSettings = await db.SystemSettings.AsNoTracking().ToListAsync(ct);
        var dbDict = dbSettings.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);

        var items = new List<SystemSettingItemDto>();

        foreach (var (key, meta) in KnownSettings)
        {
            string? rawVal = null;
            DateTimeOffset updatedAt = DateTimeOffset.UtcNow;

            if (dbDict.TryGetValue(key, out var dbRecord) && !string.IsNullOrWhiteSpace(dbRecord.Value))
            {
                rawVal = dbRecord.Value;
                updatedAt = dbRecord.UpdatedAt;
            }
            else
            {
                rawVal = await GetSettingAsync(key, meta.Default, ct);
            }

            var isConfigured = !string.IsNullOrWhiteSpace(rawVal);
            var displayVal = rawVal;

            // Mask secrets if configured
            if (meta.IsSecret && isConfigured)
            {
                displayVal = MaskSecret(rawVal!);
            }

            items.Add(new SystemSettingItemDto(
                Key: key,
                Value: displayVal,
                Category: meta.Category,
                Description: meta.Description,
                IsSecret: meta.IsSecret,
                IsConfigured: isConfigured,
                UpdatedAt: updatedAt
            ));
        }

        return new SystemSettingsResponseDto(diagnostics, items);
    }

    public void InvalidateCache()
    {
        foreach (var key in KnownSettings.Keys)
        {
            cache.Remove(CachePrefix + key);
        }
    }

    private static string MaskSecret(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= 8) return "••••••••";
        return $"{value[..4]}••••••••{value[^4..]}";
    }
}
