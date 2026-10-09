namespace NigerianNewGrid.Services;

/// <summary>
/// Service abstraction for user notification, briefing, keyword monitoring, and sync preferences.
/// Replaces static untestable Preferences access (Issue #17).
/// </summary>
public interface IUserPreferencesService
{
    bool BackgroundUpdatesEnabled { get; set; }
    bool MorningBriefingEnabled { get; set; }
    TimeSpan MorningBriefingTime { get; set; }
    bool AudioBriefingsEnabled { get; set; }
    string LastAudioBriefingCycleNotified { get; set; }
    string GetCurrentAudioBriefingCycle(DateTime? utcNow = null);
    DateTime GetNextAudioBriefingUtc(DateTime? utcNow = null);
    bool KeywordAlertsEnabled { get; set; }
    List<string> MonitoredKeywords { get; set; }
    HashSet<string> NotifiedArticleIds { get; set; }
    DateTime LastKeywordAlertEvaluationUtc { get; set; }
    DateTime LastBackgroundSyncUtc { get; set; }
    bool AddKeyword(string keyword);
    bool RemoveKeyword(string keyword);
    void RecordNotifiedArticle(string articleId);
}
