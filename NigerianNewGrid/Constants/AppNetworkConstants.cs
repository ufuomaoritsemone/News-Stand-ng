namespace NigerianNewGrid.Constants;

/// <summary>
/// Centralized API endpoint and network defaults.
/// Avoids scattering hardcoded localhost or platform-specific URLs across ViewModels and pages (Fix #13).
/// </summary>
public static class AppNetworkConstants
{
    public const string ProductionApiUrl = "https://api.nigeriannewsgrid.com";

    public static string DefaultLocalApiUrl => DeviceInfo.Platform == DevicePlatform.Android
        ? "http://10.0.2.2:56193"
        : "http://localhost:56193";

    /// <summary>
    /// Returns the user-configured API URL from Preferences, or the platform-specific default.
    /// </summary>
    public static string GetConfiguredApiBaseUrl()
    {
        var saved = Preferences.Get(AppPreferenceKeys.ApiBaseUrl, string.Empty);
        if (!string.IsNullOrWhiteSpace(saved))
        {
            return saved;
        }

        return DefaultLocalApiUrl;
    }
}
