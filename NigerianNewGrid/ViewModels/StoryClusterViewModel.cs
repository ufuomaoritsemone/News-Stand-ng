using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NigerianNewsGrid.Client;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Models;
using NigerianNewGrid.Services;

namespace NigerianNewGrid.ViewModels;

/// <summary>
/// ViewModel for the native Multi-Source Story Cluster comparison page on .NET MAUI.
/// Displays the aggregated story headline, AI summary, source count badge, and all
/// competing publisher perspectives so users can compare coverage side-by-side.
/// </summary>
public partial class StoryClusterViewModel : ObservableObject
{
    private readonly NewsApiClient _apiClient;
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<StoryClusterViewModel> _logger;

    [ObservableProperty]
    public partial string Slug { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSummary { get; set; }

    [ObservableProperty]
    public partial string? ImageUrl { get; set; }

    [ObservableProperty]
    public partial bool HasImage { get; set; }

    [ObservableProperty]
    public partial string Category { get; set; } = "Trending";

    [ObservableProperty]
    public partial int SourceCount { get; set; }

    [ObservableProperty]
    public partial int ArticleCount { get; set; }

    [ObservableProperty]
    public partial string CoverageSummaryText { get; set; } = "Multiple publishers covering this story";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public ObservableCollection<string> Sources { get; } = [];
    public ObservableCollection<StoryClusterArticleItemDto> Articles { get; } = [];

    public StoryClusterViewModel(
        NewsApiClient apiClient,
        IAnalyticsService analyticsService,
        ILogger<StoryClusterViewModel> logger)
    {
        _apiClient = apiClient;
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task InitializeFromTrendingStoryAsync(TrendingStoryItem story)
    {
        ArgumentNullException.ThrowIfNull(story);

        Slug = !string.IsNullOrWhiteSpace(story.Slug) ? story.Slug : story.Id;
        Title = story.Title;
        Summary = story.Summary ?? string.Empty;
        HasSummary = !string.IsNullOrWhiteSpace(Summary);
        ImageUrl = story.ImageUrl;
        HasImage = !string.IsNullOrWhiteSpace(ImageUrl);
        Category = !string.IsNullOrWhiteSpace(story.Category) ? story.Category : "Trending";
        SourceCount = story.SourceCount;
        ArticleCount = story.ArticleCount;
        UpdateCoverageSummary();

        Sources.Clear();
        foreach (var src in story.Sources)
        {
            Sources.Add(src);
        }

        await LoadClusterDetailsAsync();
    }

    public async Task InitializeFromSlugAsync(string slugOrId)
    {
        if (string.IsNullOrWhiteSpace(slugOrId)) return;
        Slug = slugOrId.Trim();
        await LoadClusterDetailsAsync();
    }

    [RelayCommand]
    public async Task LoadClusterDetailsAsync()
    {
        if (string.IsNullOrWhiteSpace(Slug) || IsBusy) return;

        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;

            var detail = await _apiClient.GetStoryClusterBySlugAsync(Slug);
            if (detail is null)
            {
                if (await TryPopulateFromLocalCacheAsync())
                {
                    return;
                }

                if (Articles.Count == 0)
                {
                    HasError = true;
                    ErrorMessage = "Unable to load publisher perspectives right now. Pull down to retry.";
                }
                return;
            }

            Title = !string.IsNullOrWhiteSpace(detail.Title) ? detail.Title : Title;
            if (!string.IsNullOrWhiteSpace(detail.Summary))
            {
                Summary = detail.Summary;
                HasSummary = true;
            }

            if (!string.IsNullOrWhiteSpace(detail.ImageUrl))
            {
                ImageUrl = detail.ImageUrl;
                HasImage = true;
            }

            SourceCount = detail.SourceCount > 0 ? detail.SourceCount : detail.Sources.Count;
            ArticleCount = detail.ArticleCount > 0 ? detail.ArticleCount : detail.Articles.Count;
            UpdateCoverageSummary();

            if (detail.Sources.Count > 0)
            {
                Sources.Clear();
                foreach (var src in detail.Sources)
                {
                    Sources.Add(src);
                }
            }

            Articles.Clear();
            foreach (var article in detail.Articles)
            {
                Articles.Add(article);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[StoryClusterViewModel] Error loading cluster '{Slug}'", Slug);
            if (Articles.Count == 0 && !await TryPopulateFromLocalCacheAsync())
            {
                HasError = true;
                ErrorMessage = "Could not connect to News Stand NG. Check your connection and try again.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> TryPopulateFromLocalCacheAsync()
    {
        try
        {
            var cacheService = IPlatformApplication.Current?.Services.GetService<IBriefingCacheService>();
            if (cacheService is null) return false;

            var cachedCategories = await cacheService.GetCachedBriefingAsync(maxTotalStories: 60);
            var allCached = cachedCategories.SelectMany(c => c.Top).Where(a => !a.IsAdMobPlaceholder).ToList();
            if (allCached.Count == 0) return false;

            var matched = allCached
                .Where(a => !string.IsNullOrWhiteSpace(Category) &&
                            string.Equals(a.Category?.Trim(), Category.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(a => a.PublishedAt ?? DateTime.MinValue)
                .Take(8)
                .ToList();

            if (matched.Count == 0)
            {
                matched = allCached.OrderByDescending(a => a.PublishedAt ?? DateTime.MinValue).Take(5).ToList();
            }

            Articles.Clear();
            foreach (var item in matched)
            {
                Articles.Add(new StoryClusterArticleItemDto
                {
                    ArticleId = item.Id,
                    Title = item.Title,
                    Slug = item.Slug,
                    Excerpt = item.Summary,
                    Url = item.Url ?? string.Empty,
                    ImageUrl = item.ImageUrl,
                    Source = !string.IsNullOrWhiteSpace(item.Source) ? item.Source : "News Stand NG",
                    PublishedAt = item.PublishedAt.HasValue
                        ? new DateTimeOffset(DateTime.SpecifyKind(item.PublishedAt.Value, DateTimeKind.Utc))
                        : DateTimeOffset.UtcNow,
                    SimilarityScore = 0.85f
                });
            }

            var distinctSources = Articles
                .Select(a => a.Source)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (distinctSources.Count > 0)
            {
                Sources.Clear();
                foreach (var src in distinctSources)
                {
                    Sources.Add(src);
                }
            }

            ArticleCount = Articles.Count;
            SourceCount = Math.Max(2, Sources.Count);
            UpdateCoverageSummary();
            return Articles.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    private async Task OpenPerspectiveArticleAsync(StoryClusterArticleItemDto? article)
    {
        if (article is null || string.IsNullOrWhiteSpace(article.Url)) return;

        _ = _analyticsService.TrackAsync("story_cluster_article_click", article.ArticleId, article.Title, Category);

        var page = new ArticleWebPage(new ArticleNavigationParams(
            Url: article.Url,
            Title: article.Title,
            ImageUrl: !string.IsNullOrWhiteSpace(article.ImageUrl) ? article.ImageUrl : ImageUrl,
            Category: Category,
            ArticleId: article.ArticleId,
            Summary: article.Excerpt,
            Author: article.Source,
            Slug: article.Slug));

        var nav = Shell.Current?.Navigation
            ?? Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation;
        if (nav is not null)
        {
            await nav.PushAsync(page);
        }
    }

    [RelayCommand]
    private async Task SearchMoreCoverageAsync()
    {
        if (string.IsNullOrWhiteSpace(Title)) return;
        var encoded = Uri.EscapeDataString(Title);
        if (Shell.Current is not null)
        {
            await Shell.Current.GoToAsync($"//discover?query={encoded}");
        }
    }

    private void UpdateCoverageSummary()
    {
        var sourcesLabel = SourceCount == 1 ? "1 publisher" : $"{Math.Max(1, SourceCount)} publishers";
        var articlesLabel = ArticleCount == 1 ? "1 report" : $"{Math.Max(1, ArticleCount)} reports";
        CoverageSummaryText = $"{sourcesLabel} · {articlesLabel} covering this developing story";
    }
}
