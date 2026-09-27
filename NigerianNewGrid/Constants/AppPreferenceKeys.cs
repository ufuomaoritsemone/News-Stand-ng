namespace NigerianNewGrid.Constants;

/// <summary>
/// Single source of truth for all Preferences keys used throughout the MAUI app.
/// Eliminates magic strings scattered across multiple files (Fix #35).
/// </summary>
public static class AppPreferenceKeys
{
    // ── Onboarding ───────────────────────────────────────────────
    public const string HasSeenOnboarding   = "has_seen_onboarding";
    public const string PreferredLanguage   = "preferred_language";
    public const string DailyReminderEnabled = "daily_reminder_enabled";

    // ── Network ──────────────────────────────────────────────────
    public const string ApiBaseUrl          = "api_base_url";

    // ── Cache ────────────────────────────────────────────────────
    public const string LastBriefing        = "last_briefing";
    public const string LastTrendingVideos  = "last_trending_videos";
    public const string LastTopNewsVideos   = "last_top_news_videos";

    // ── User Keywords ────────────────────────────────────────────
    public const string KeywordAlerts       = "keyword_alerts";
}
