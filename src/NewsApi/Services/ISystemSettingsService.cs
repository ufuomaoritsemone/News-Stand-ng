using NewsApi.Models;

namespace NewsApi.Services;

/// <summary>
/// Provides live runtime system configuration reading and writing with in-memory caching.
/// Overrides appsettings.json and environment variables dynamically at runtime.
/// </summary>
public interface ISystemSettingsService
{
    Task<string?> GetSettingAsync(string key, string? defaultValue = null, CancellationToken ct = default);
    Task<bool> GetBoolSettingAsync(string key, bool defaultValue = false, CancellationToken ct = default);
    Task<int> GetIntSettingAsync(string key, int defaultValue = 0, CancellationToken ct = default);
    Task SetSettingAsync(string key, string? value, string category = "General", string? description = null, bool isSecret = false, CancellationToken ct = default);
    Task UpdateSettingsBatchAsync(Dictionary<string, string?> updates, CancellationToken ct = default);
    Task<SystemSettingsResponseDto> GetSettingsWithDiagnosticsAsync(CancellationToken ct = default);
    void InvalidateCache();
}
