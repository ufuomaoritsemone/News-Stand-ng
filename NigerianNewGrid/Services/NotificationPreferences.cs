namespace NigerianNewGrid.Services;

/// <summary>
/// Static facade for user preferences that delegates to <see cref="IUserPreferencesService"/>.
/// Ensures thread safety, in-memory caching, and backwards compatibility with platform broadcast receivers.
/// </summary>
public static class NotificationPreferences
{
    private static IUserPreferencesService? _instance;
    private static readonly object _initLock = new();

    public static IUserPreferencesService Service
    {
        get
        {
            if (_instance != null) return _instance;
            lock (_initLock)
            {
                if (_instance != null) return _instance;
                _instance = IPlatformApplication.Current?.Services.GetService<IUserPreferencesService>()
                            ?? new UserPreferencesService();
                return _instance;
            }
        }
        set
        {
            lock (_initLock)
            {
                _instance = value;
            }
        }
    }

    public static bool BackgroundUpdatesEnabled
    {
        get => Service.BackgroundUpdatesEnabled;
        set => Service.BackgroundUpdatesEnabled = value;
    }

    public static bool MorningBriefingEnabled
    {
        get => Service.MorningBriefingEnabled;
        set => Service.MorningBriefingEnabled = value;
    }

    public static TimeSpan MorningBriefingTime
    {
        get => Service.MorningBriefingTime;
        set => Service.MorningBriefingTime = value;
    }

    public static bool AudioBriefingsEnabled
    {
        get => Service.AudioBriefingsEnabled;
        set => Service.AudioBriefingsEnabled = value;
    }

    public static string LastAudioBriefingCycleNotified
    {
        get => Service.LastAudioBriefingCycleNotified;
        set => Service.LastAudioBriefingCycleNotified = value;
    }

    public static string GetCurrentAudioBriefingCycle(DateTime? utcNow = null)
        => Service.GetCurrentAudioBriefingCycle(utcNow);

    public static DateTime GetNextAudioBriefingUtc(DateTime? utcNow = null)
        => Service.GetNextAudioBriefingUtc(utcNow);

    public static bool KeywordAlertsEnabled
    {
        get => Service.KeywordAlertsEnabled;
        set => Service.KeywordAlertsEnabled = value;
    }

    public static List<string> MonitoredKeywords
    {
        get => Service.MonitoredKeywords;
        set => Service.MonitoredKeywords = value;
    }

    public static HashSet<string> NotifiedArticleIds
    {
        get => Service.NotifiedArticleIds;
        set => Service.NotifiedArticleIds = value;
    }

    public static DateTime LastKeywordAlertEvaluationUtc
    {
        get => Service.LastKeywordAlertEvaluationUtc;
        set => Service.LastKeywordAlertEvaluationUtc = value;
    }

    public static DateTime LastBackgroundSyncUtc
    {
        get => Service.LastBackgroundSyncUtc;
        set => Service.LastBackgroundSyncUtc = value;
    }

    public static bool AddKeyword(string keyword) => Service.AddKeyword(keyword);

    public static bool RemoveKeyword(string keyword) => Service.RemoveKeyword(keyword);

    public static void RecordNotifiedArticle(string articleId) => Service.RecordNotifiedArticle(articleId);
}
