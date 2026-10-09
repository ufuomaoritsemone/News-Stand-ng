using System.Text.Json;
using NigerianNewsGrid.Client.Helpers;

namespace NigerianNewGrid.Services;

/// <summary>
/// Thread-safe, injectable implementation of <see cref="IUserPreferencesService"/>.
/// Provides cached in-memory structures for monitored keywords and notified article IDs
/// to eliminate expensive serializations on every hit while persisting changes atomically.
/// </summary>
public class UserPreferencesService : IUserPreferencesService
{
    private const string MorningEnabledKey = "morning_briefing_enabled";
    private const string MorningTimeKey = "morning_briefing_time_mins";
    private const string AudioBriefingsEnabledKey = "audio_briefings_enabled";
    private const string LastAudioBriefingCycleKey = "last_audio_briefing_cycle_notified";
    private const string KeywordAlertsEnabledKey = "keyword_alerts_enabled";
    private const string MonitoredKeywordsKey = "monitored_keywords";
    private const string NotifiedArticleIdsKey = "notified_article_ids";
    private const string LastAlertEvaluationTimeKey = "last_keyword_alert_eval_utc";
    private const string LastSyncTimeKey = "last_background_sync_utc";
    private const string BackgroundUpdatesEnabledKey = "background_updates_enabled";

    private static JsonSerializerOptions JsonOptions => SharedJsonOptions.Default;

    private readonly object _lock = new();
    private List<string>? _cachedKeywords;
    private HashSet<string>? _cachedNotifiedArticleIds;

    public bool BackgroundUpdatesEnabled
    {
        get => Preferences.Get(BackgroundUpdatesEnabledKey, true);
        set => Preferences.Set(BackgroundUpdatesEnabledKey, value);
    }

    public bool MorningBriefingEnabled
    {
        get => Preferences.Get(MorningEnabledKey, true);
        set => Preferences.Set(MorningEnabledKey, value);
    }

    public TimeSpan MorningBriefingTime
    {
        get => TimeSpan.FromMinutes(Preferences.Get(MorningTimeKey, 450)); // 7:30 AM default (450 minutes)
        set => Preferences.Set(MorningTimeKey, (int)value.TotalMinutes);
    }

    public bool AudioBriefingsEnabled
    {
        get => Preferences.Get(AudioBriefingsEnabledKey, true);
        set => Preferences.Set(AudioBriefingsEnabledKey, value);
    }

    public string LastAudioBriefingCycleNotified
    {
        get => Preferences.Get(LastAudioBriefingCycleKey, string.Empty);
        set => Preferences.Set(LastAudioBriefingCycleKey, value);
    }

    public string GetCurrentAudioBriefingCycle(DateTime? utcNow = null)
        => TtsBriefingFormatter.GetCurrentAudioBriefingCycle(utcNow);

    public DateTime GetNextAudioBriefingUtc(DateTime? utcNow = null)
        => TtsBriefingFormatter.GetNextAudioBriefingUtc(utcNow);

    public bool KeywordAlertsEnabled
    {
        get => Preferences.Get(KeywordAlertsEnabledKey, false);
        set
        {
            if (value && !Preferences.ContainsKey(LastAlertEvaluationTimeKey))
            {
                LastKeywordAlertEvaluationUtc = DateTime.UtcNow;
            }
            Preferences.Set(KeywordAlertsEnabledKey, value);
        }
    }

    public List<string> MonitoredKeywords
    {
        get
        {
            lock (_lock)
            {
                if (_cachedKeywords != null)
                {
                    return new List<string>(_cachedKeywords);
                }

                var json = Preferences.Get(MonitoredKeywordsKey, string.Empty);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _cachedKeywords = ["Naira", "Tinubu", "EFCC"];
                }
                else
                {
                    try
                    {
                        _cachedKeywords = JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? ["Naira", "Tinubu", "EFCC"];
                    }
                    catch
                    {
                        _cachedKeywords = ["Naira", "Tinubu", "EFCC"];
                    }
                }

                return new List<string>(_cachedKeywords);
            }
        }
        set
        {
            lock (_lock)
            {
                _cachedKeywords = new List<string>(value);
                Preferences.Set(MonitoredKeywordsKey, JsonSerializer.Serialize(_cachedKeywords, JsonOptions));
            }
        }
    }

    public HashSet<string> NotifiedArticleIds
    {
        get
        {
            lock (_lock)
            {
                if (_cachedNotifiedArticleIds != null)
                {
                    return new HashSet<string>(_cachedNotifiedArticleIds);
                }

                var json = Preferences.Get(NotifiedArticleIdsKey, string.Empty);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _cachedNotifiedArticleIds = [];
                }
                else
                {
                    try
                    {
                        _cachedNotifiedArticleIds = JsonSerializer.Deserialize<HashSet<string>>(json, JsonOptions) ?? [];
                    }
                    catch
                    {
                        _cachedNotifiedArticleIds = [];
                    }
                }

                return new HashSet<string>(_cachedNotifiedArticleIds);
            }
        }
        set
        {
            lock (_lock)
            {
                _cachedNotifiedArticleIds = new HashSet<string>(value);
                Preferences.Set(NotifiedArticleIdsKey, JsonSerializer.Serialize(_cachedNotifiedArticleIds, JsonOptions));
            }
        }
    }

    public DateTime LastKeywordAlertEvaluationUtc
    {
        get => DateTime.TryParse(Preferences.Get(LastAlertEvaluationTimeKey, string.Empty), out var dt)
            ? dt
            : DateTime.UtcNow;
        set => Preferences.Set(LastAlertEvaluationTimeKey, value.ToString("o"));
    }

    public DateTime LastBackgroundSyncUtc
    {
        get => DateTime.TryParse(Preferences.Get(LastSyncTimeKey, string.Empty), out var dt)
            ? dt
            : DateTime.UtcNow.AddHours(-6);
        set => Preferences.Set(LastSyncTimeKey, value.ToString("o"));
    }

    public bool AddKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return false;
        var trimmed = keyword.Trim();

        lock (_lock)
        {
            var current = MonitoredKeywords;
            if (current.Any(k => string.Equals(k, trimmed, StringComparison.OrdinalIgnoreCase)))
                return false;

            current.Add(trimmed);
            MonitoredKeywords = current;
            return true;
        }
    }

    public bool RemoveKeyword(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return false;
        var trimmed = keyword.Trim();

        lock (_lock)
        {
            var current = MonitoredKeywords;
            var removed = current.RemoveAll(k => string.Equals(k, trimmed, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
            {
                MonitoredKeywords = current;
            }
            return removed;
        }
    }

    public void RecordNotifiedArticle(string articleId)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return;

        lock (_lock)
        {
            var current = NotifiedArticleIds;
            if (current.Add(articleId))
            {
                if (current.Count > 500)
                {
                    current = current.Skip(current.Count - 400).ToHashSet();
                }
                NotifiedArticleIds = current;
            }
        }
    }
}
