namespace NigerianNewGrid.Constants
{
    /// <summary>
    /// Centralized Google AdMob App IDs and Ad Unit IDs for Android and iOS.
    /// In DEBUG mode, test ad unit IDs or test mode can be used to prevent policy violations during development.
    /// In RELEASE mode, official production ad unit IDs are served.
    /// </summary>
    public static class AdConstants
    {
        // ══════════════════════════════════════════════
        // Google AdMob App IDs
        // ══════════════════════════════════════════════
        public const string AndroidAppId = "ca-app-pub-0810356418854639~3397453941";
        public const string IosAppId = "ca-app-pub-0810356418854639~7145127262";

        // ══════════════════════════════════════════════
        // Production Ad Unit IDs
        // ══════════════════════════════════════════════
        public const string AndroidBannerAdUnitId = "ca-app-pub-0810356418854639/7601009567";
        public const string AndroidNativeAdUnitId = "ca-app-pub-0810356418854639/8492878270";

        public const string IosBannerAdUnitId = "ca-app-pub-0810356418854639/2200382762";
        public const string IosNativeAdUnitId = "ca-app-pub-0810356418854639/5209689482";

        // ══════════════════════════════════════════════
        // Google Official Sample Test Ad Unit IDs (for safe local development)
        // ══════════════════════════════════════════════
        public const string AndroidTestBannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";
        public const string AndroidTestNativeAdUnitId = "ca-app-pub-3940256099942544/2247696110";

        public const string IosTestBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
        public const string IosTestNativeAdUnitId = "ca-app-pub-3940256099942544/3986624511";

        /// <summary>
        /// Resolved Banner Ad Unit ID based on platform and build configuration.
        /// </summary>
        public static string BannerAdUnitId
        {
            get
            {
#if DEBUG
                // In Debug mode, use test ad unit IDs to avoid AdMob policy violations
#if ANDROID
                return AndroidTestBannerAdUnitId;
#elif IOS
                return IosTestBannerAdUnitId;
#else
                return AndroidTestBannerAdUnitId;
#endif
#else
                // In Release mode, use production IDs
#if ANDROID
                return AndroidBannerAdUnitId;
#elif IOS
                return IosBannerAdUnitId;
#else
                return AndroidBannerAdUnitId;
#endif
#endif
            }
        }

        /// <summary>
        /// Resolved Native Ad Unit ID based on platform and build configuration.
        /// </summary>
        public static string NativeAdUnitId
        {
            get
            {
#if DEBUG
                // In Debug mode, use test ad unit IDs to avoid AdMob policy violations
#if ANDROID
                return AndroidTestNativeAdUnitId;
#elif IOS
                return IosTestNativeAdUnitId;
#else
                return AndroidTestNativeAdUnitId;
#endif
#else
                // In Release mode, use production IDs
#if ANDROID
                return AndroidNativeAdUnitId;
#elif IOS
                return IosNativeAdUnitId;
#else
                return AndroidNativeAdUnitId;
#endif
#endif
            }
        }
    }
}
