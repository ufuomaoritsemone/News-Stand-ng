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
        public const string AndroidVideoNativeAdUnitId = "ca-app-pub-0810356418854639/3806906659";
        public const string AndroidAppOpenAdUnitId = "ca-app-pub-0810356418854639/1945824796";

        public const string IosBannerAdUnitId = "ca-app-pub-0810356418854639/2200382762";
        public const string IosNativeAdUnitId = "ca-app-pub-0810356418854639/5209689482";
        public const string IosVideoNativeAdUnitId = "ca-app-pub-0810356418854639/5675356246";
        public const string IosAppOpenAdUnitId = "ca-app-pub-0810356418854639/5157563062";

        // ══════════════════════════════════════════════
        // Google Official Sample Test Ad Unit IDs (for safe local development)
        // ══════════════════════════════════════════════
        public const string AndroidTestBannerAdUnitId = "ca-app-pub-3940256099942544/6300978111";
        public const string AndroidTestNativeAdUnitId = "ca-app-pub-3940256099942544/2247696110";
        public const string AndroidTestVideoNativeAdUnitId = "ca-app-pub-3940256099942544/2247696110"; // reuse native test ID
        public const string AndroidTestAppOpenAdUnitId = "ca-app-pub-3940256099942544/9257395921";

        public const string IosTestBannerAdUnitId = "ca-app-pub-3940256099942544/2934735716";
        public const string IosTestNativeAdUnitId = "ca-app-pub-3940256099942544/3986624511";
        public const string IosTestVideoNativeAdUnitId = "ca-app-pub-3940256099942544/3986624511"; // reuse native test ID
        public const string IosTestAppOpenAdUnitId = "ca-app-pub-3940256099942544/5604853255";

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

        /// <summary>
        /// Resolved Native Ad Unit ID for the Video News section (standalone placement).
        /// </summary>
        public static string VideoNativeAdUnitId
        {
            get
            {
#if DEBUG
#if ANDROID
                return AndroidTestVideoNativeAdUnitId;
#elif IOS
                return IosTestVideoNativeAdUnitId;
#else
                return AndroidTestVideoNativeAdUnitId;
#endif
#else
#if ANDROID
                return AndroidVideoNativeAdUnitId;
#elif IOS
                return IosVideoNativeAdUnitId;
#else
                return AndroidVideoNativeAdUnitId;
#endif
#endif
            }
        }

        /// <summary>
        /// Resolved App Open Ad Unit ID based on platform and build configuration.
        /// </summary>
        public static string AppOpenAdUnitId
        {
            get
            {
#if DEBUG
                // In Debug mode, use test ad unit IDs to avoid AdMob policy violations
#if ANDROID
                return AndroidTestAppOpenAdUnitId;
#elif IOS
                return IosTestAppOpenAdUnitId;
#else
                return AndroidTestAppOpenAdUnitId;
#endif
#else
                // In Release mode, use production IDs
#if ANDROID
                return AndroidAppOpenAdUnitId;
#elif IOS
                return IosAppOpenAdUnitId;
#else
                return AndroidAppOpenAdUnitId;
#endif
#endif
            }
        }
    }
}
