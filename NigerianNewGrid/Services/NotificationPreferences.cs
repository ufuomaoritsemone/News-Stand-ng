using System.Text.Json;

namespace NigerianNewGrid.Services;

/// <summary>
/// Manages on-device preferences for morning briefings, monitored keywords, and alert state.
/// All data remains strictly local to the device for privacy preservation.
/// </summary>
public static class NotificationPreferences
{
    private const string MorningEnabledKey = "morning_briefing_enabled";
    private const string MorningTimeKey = "morning_briefing_time_mins";
    private const string KeywordAlertsEnabledKey = "keyword_alerts_enabled";
    private const string MonitoredKeywordsKey = "monitored_keywords";
    private const string NotifiedArticleIdsKey = "notified_article_ids";
    private const string LastAlertEvaluationTimeKey = "last_keyword_alert_eval_utc";
    private const string LastSyncTimeKey = "last_background_sync_utc";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool MorningBriefingEnabled
    {
        get => Preferences.Get(MorningEnabledKey, true);
        set => Preferences.Set(MorningEnabledKey, value);
    }

    public static TimeSpan MorningBriefingTime
    {
        get => TimeSpan.FromMinutes(Preferences.Get(MorningTimeKey, 450)); // 7:30 AM default (450 minutes)
        set => Preferences.Set(MorningTimeKey, (int)value.TotalMinutes);
    }

    public static bool KeywordAlertsEnabled
    {
        get => Preferences.Get(KeywordAlertsEnabledKey, false);
        set
        {
            // When enabling for the first time or re-enabling, anchor the evaluation baseline
            // to current UTC time so old/backlog stories do not trigger historical alerts.
            if (value && !Preferences.ContainsKey(LastAlertEvaluationTimeKey))
            {
                LastKeywordAlertEvaluationUtc = DateTime.UtcNow;
            }
            Preferences.Set(KeywordAlertsEnabledKey, value);
        }
    }

    public static List<string> MonitoredKeywords
    {
        get
        {
            var json = Preferences.Get(MonitoredKeywordsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return ["Naira", "Tinubu", "EFCC"];
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? ["Naira", "Tinubu", "EFCC"];
            }
            catch
            {
                return ["Naira", "Tinubu", "EFCC"];
            }
        }
        set => Preferences.Set(MonitoredKeywordsKey, JsonSerializer.Serialize(value, JsonOptions));
    }

    public static HashSet<string> NotifiedArticleIds
    {
        get
        {
            var json = Preferences.Get(NotifiedArticleIdsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<HashSet<string>>(json, JsonOptions) ?? [];
            }
            catch
            {
                return [];
            }
        }
        set => Preferences.Set(NotifiedArticleIdsKey, JsonSerializer.Serialize(value, JsonOptions));
    }

    public static DateTime LastKeywordAlertEvaluationUtc
    {
        get => DateTime.TryParse(Preferences.Get(LastAlertEvaluationTimeKey, string.Empty), out var dt)
            ? dt
            : DateTime.UtcNow;
        set => Preferences.Set(LastAlertEvaluationTimeKey, value.ToString("o"));
    }

    public static DateTime LastBackgroundSyncUtc
    {
        get => DateTime.TryParse(Preferences.Get(LastSyncTimeKey, string.Empty), out var dt)
            ? dt
            : DateTime.UtcNow.AddHours(-6);
        set => Preferences.Set(LastSyncTimeKey, value.ToString("o"));
    }

    public static bool AddKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return false;
        var trimmed = keyword.Trim();
        var current = MonitoredKeywords;

        if (current.Any(k => string.Equals(k, trimmed, StringComparison.OrdinalIgnoreCase)))
            return false;

        current.Add(trimmed);
        MonitoredKeywords = current;
        return true;
    }

    public static bool RemoveKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return false;
        var trimmed = keyword.Trim();
        var current = MonitoredKeywords;

        var removed = current.RemoveAll(k => string.Equals(k, trimmed, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed)
        {
            MonitoredKeywords = current;
        }
        return removed;
    }

    public static void RecordNotifiedArticle(string articleId)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return;
        var current = NotifiedArticleIds;
        if (current.Add(articleId))
        {
            // Keep cache bounded to last 500 notified IDs to prevent unbounded preference growth
            if (current.Count > 500)
            {
                current = current.Skip(current.Count - 400).ToHashSet();
            }
            NotifiedArticleIds = current;
        }
    }
}
