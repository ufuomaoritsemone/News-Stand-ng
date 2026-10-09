using Microsoft.Extensions.Logging;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

public class KeywordAlertCoordinator : IKeywordAlertCoordinator
{
    private readonly IKeywordMatchingService _keywordMatchingService;
    private readonly IUserPreferencesService _userPreferences;
    private readonly INotificationService _notificationService;
    private readonly ILogger<KeywordAlertCoordinator> _logger;

    public KeywordAlertCoordinator(
        IKeywordMatchingService keywordMatchingService,
        IUserPreferencesService userPreferences,
        INotificationService notificationService,
        ILogger<KeywordAlertCoordinator> logger)
    {
        _keywordMatchingService = keywordMatchingService;
        _userPreferences = userPreferences;
        _notificationService = notificationService;
        _logger = logger;
    }

    public void EvaluateKeywordAlerts(IReadOnlyList<BriefingCategory> categories)
    {
        if (!_userPreferences.KeywordAlertsEnabled) return;

        try
        {
            var allStories = categories
                .SelectMany(c => c.Top)
                .Where(s => !string.IsNullOrWhiteSpace(s.Id))
                .ToList();

            var matches = _keywordMatchingService.EvaluateFreshArticles(
                allStories,
                _userPreferences.MonitoredKeywords,
                excludedArticleIds: _userPreferences.NotifiedArticleIds);

            foreach (var match in matches)
            {
                _userPreferences.RecordNotifiedArticle(match.Article.Id);
                _notificationService.ShowKeywordAlertNotification(
                    match.MatchedKeyword, match.Article.Title,
                    match.Article.Id, match.Article.Url, match.Article.Category);
            }

            if (matches.Count > 0)
                _userPreferences.LastKeywordAlertEvaluationUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keyword evaluation error.");
        }
    }
}
