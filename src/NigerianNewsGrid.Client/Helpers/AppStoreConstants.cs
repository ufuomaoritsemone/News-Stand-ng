namespace NigerianNewsGrid.Client.Helpers;

/// <summary>
/// Centralized application store links and configuration.
/// Set these constants once your mobile app is published to the respective app stores.
/// When left empty (""), the 𝕏 (Twitter) share system will cleanly omit the store download CTA
/// and share only the news headline, publisher attribution, and story URL.
/// </summary>
public static class AppStoreConstants
{
    /// <summary>
    /// Google Play Store URL for News Stand NG (Android).
    /// Example: "https://play.google.com/store/apps/details?id=com.nigeriannewsgrid.app"
    /// Leave empty until the app is approved and live on Google Play.
    /// </summary>
    public const string GooglePlayStoreUrl = "";

    /// <summary>
    /// Apple App Store URL for News Stand NG (iOS).
    /// Example: "https://apps.apple.com/app/news-stand-ng/id1234567890"
    /// Leave empty until the app is approved and live on the Apple App Store.
    /// </summary>
    public const string AppleAppStoreUrl = "";

    /// <summary>
    /// Returns true if a live Google Play Store URL has been configured.
    /// </summary>
    public static bool HasGooglePlayStoreUrl => !string.IsNullOrWhiteSpace(GooglePlayStoreUrl);

    /// <summary>
    /// Returns true if a live Apple App Store URL has been configured.
    /// </summary>
    public static bool HasAppleAppStoreUrl => !string.IsNullOrWhiteSpace(AppleAppStoreUrl);
}
