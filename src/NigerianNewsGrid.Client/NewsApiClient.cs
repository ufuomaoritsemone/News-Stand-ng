using System.Net.Http.Json;
using System.Text.Json;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewsGrid.Client;

public class NewsApiClient
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // Prefer HttpClient via DI (IHttpClientFactory / typed client)
    public NewsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string BaseUrl { get; set; } = "http://localhost:56193";

    public async Task<List<BriefingCategory>> GetDailyBriefingAsync(string? language = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var builder = new UriBuilder(new Uri(new Uri(normalizedBaseUrl), "api/v1/articles/briefings"));
            var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
            if (!string.IsNullOrWhiteSpace(language))
            {
                query["language"] = language;
            }
            builder.Query = query.ToString();

            // Debug write the request url  
            var requestUrl = builder.Uri.ToString();
            System.Diagnostics.Debug.WriteLine($"📡 [NewsApiClient] Requesting: {requestUrl}");

            var response = await _httpClient.GetAsync(builder.Uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"❌ [NewsApiClient] HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
                return new List<BriefingCategory>();
            }

            var categories = await response.Content.ReadFromJsonAsync<List<BriefingCategory>>(JsonOptions, cancellationToken);
            System.Diagnostics.Debug.WriteLine($"✅ [NewsApiClient] Successfully loaded {categories?.Count ?? 0} categories");

            // Warn if the scraper hasn't refreshed data recently
            if (response.Headers.TryGetValues("X-Data-Age-Hours", out var ageValues)
                && double.TryParse(ageValues.FirstOrDefault(), out var ageHours)
                && ageHours > 24)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ [NewsApiClient] Data is {ageHours:F0} hours old — scraper may be down");
            }

            return categories ?? new List<BriefingCategory>();
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected — propagate it cleanly
            System.Diagnostics.Debug.WriteLine("⏱️ [NewsApiClient] Request cancelled by user");
            throw;
        }
        catch (HttpRequestException ex)
        {
            // Network or HTTP error — return empty to let the caller show cached data
            System.Diagnostics.Debug.WriteLine($"❌ Network error: {ex.Message}");
            return new List<BriefingCategory>();
        }
        catch (JsonException ex)
        {
            // Malformed API response — return empty
            System.Diagnostics.Debug.WriteLine($"❌ JSON parse error: {ex.Message}");
            return new List<BriefingCategory>();
        }
    }

    /// <summary>
    /// Verifies whether curated audio headlines / daily briefing stories are currently available.
    /// Used by notification receivers and schedulers to avoid dispatching alerts when no headlines exist.
    /// </summary>
    public async Task<bool> CheckAudioHeadlinesAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var uri = new Uri(new Uri(normalizedBaseUrl), "api/v1/articles/briefings/availability");

            var response = await _httpClient.GetAsync(uri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<BriefingAvailabilityResult>(JsonOptions, cancellationToken);
                return result?.Available ?? false;
            }

            // Fallback: probe briefings endpoint directly
            var categories = await GetDailyBriefingAsync(cancellationToken: cancellationToken);
            return categories.Any(c => c.Top.Count > 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ [NewsApiClient] Availability check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Fetches metadata and streaming URL for the latest high-definition audio news briefing (morning or evening WAT).
    /// </summary>
    public async Task<AudioBriefingMetadata?> GetLatestAudioBriefingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var uri = new Uri(new Uri(normalizedBaseUrl), "api/v1/audio/briefings/latest");

            var response = await _httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<AudioBriefingMetadata>(JsonOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ [NewsApiClient] Failed to fetch latest audio briefing: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Resolves a relative stream URL (e.g. /api/v1/audio/briefing_morning.mp3) to an absolute URL against BaseUrl.
    /// </summary>
    public string GetAbsoluteAudioUrl(string relativeOrAbsoluteUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsoluteUrl)) return string.Empty;
        if (Uri.TryCreate(relativeOrAbsoluteUrl, UriKind.Absolute, out var absUri))
        {
            return absUri.ToString();
        }

        var normalizedBase = NormalizeBaseUrl(BaseUrl);
        return new Uri(new Uri(normalizedBase), relativeOrAbsoluteUrl.TrimStart('/')).ToString();
    }

    public async Task<List<VideoStoryItem>> GetVideoStoriesAsync(int limit = 20, string? category = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var builder = new UriBuilder(new Uri(new Uri(normalizedBaseUrl), "api/v1/video-stories"));
            var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
            query["limit"] = limit.ToString();
            if (!string.IsNullOrWhiteSpace(category)) query["category"] = category;
            builder.Query = query.ToString();

            var response = await _httpClient.GetAsync(builder.Uri, cancellationToken);
            if (!response.IsSuccessStatusCode) return new List<VideoStoryItem>();

            var stories = await response.Content.ReadFromJsonAsync<List<VideoStoryItem>>(JsonOptions, cancellationToken);
            return stories ?? new List<VideoStoryItem>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"❌ [NewsApiClient] Failed to fetch video stories: {ex.Message}");
            return new List<VideoStoryItem>();
        }
    }

    public async Task<List<VideoStoryItem>> GetTrendingVideoStoriesAsync(int limit = 20, string? category = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var builder = new UriBuilder(new Uri(new Uri(normalizedBaseUrl), "api/v1/video-stories/trending"));
            var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
            query["limit"] = limit.ToString();
            if (!string.IsNullOrWhiteSpace(category)) query["category"] = category;
            builder.Query = query.ToString();

            var response = await _httpClient.GetAsync(builder.Uri, cancellationToken);
            if (!response.IsSuccessStatusCode) return new List<VideoStoryItem>();

            var stories = await response.Content.ReadFromJsonAsync<List<VideoStoryItem>>(JsonOptions, cancellationToken);
            return stories ?? new List<VideoStoryItem>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"❌ [NewsApiClient] Failed to fetch trending video stories: {ex.Message}");
            return new List<VideoStoryItem>();
        }
    }

    public async Task<List<SocialPostItem>> GetSocialPostsAsync(int limit = 20, string? category = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var builder = new UriBuilder(new Uri(new Uri(normalizedBaseUrl), "api/v1/social-posts"));
            var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
            query["limit"] = limit.ToString();
            if (!string.IsNullOrWhiteSpace(category)) query["category"] = category;
            builder.Query = query.ToString();

            var response = await _httpClient.GetAsync(builder.Uri, cancellationToken);
            if (!response.IsSuccessStatusCode) return new List<SocialPostItem>();

            var posts = await response.Content.ReadFromJsonAsync<List<SocialPostItem>>(JsonOptions, cancellationToken);
            return posts ?? new List<SocialPostItem>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"❌ [NewsApiClient] Failed to fetch social posts: {ex.Message}");
            return new List<SocialPostItem>();
        }
    }

    public async Task<List<VideoFeedItem>> GetVideoChannelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var uri = new Uri(new Uri(normalizedBaseUrl), "api/v1/video-channels");
            var response = await _httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode) return new List<VideoFeedItem>();

            var channels = await response.Content.ReadFromJsonAsync<List<VideoFeedItem>>(JsonOptions, cancellationToken);
            return channels ?? new List<VideoFeedItem>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"❌ [NewsApiClient] Failed to fetch video channels: {ex.Message}");
            return new List<VideoFeedItem>();
        }
    }

    public async Task<List<SocialFeedItem>> GetSocialHandlesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var uri = new Uri(new Uri(normalizedBaseUrl), "api/v1/social-handles");
            var response = await _httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode) return new List<SocialFeedItem>();

            var handles = await response.Content.ReadFromJsonAsync<List<SocialFeedItem>>(JsonOptions, cancellationToken);
            return handles ?? new List<SocialFeedItem>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"❌ [NewsApiClient] Failed to fetch social handles: {ex.Message}");
            return new List<SocialFeedItem>();
        }
    }

    public async Task<RelatedStoriesResult> GetRelatedStoriesAsync(
        string? articleId = null,
        string? title = null,
        string? category = null,
        int limit = 4,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var builder = new UriBuilder(new Uri(new Uri(normalizedBaseUrl), "api/v1/articles/related"));
            var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
            if (!string.IsNullOrWhiteSpace(articleId)) query["id"] = articleId;
            if (!string.IsNullOrWhiteSpace(title)) query["title"] = title;
            if (!string.IsNullOrWhiteSpace(category)) query["category"] = category;
            query["limit"] = limit.ToString();
            builder.Query = query.ToString();

            var response = await _httpClient.GetAsync(builder.Uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new RelatedStoriesResult();

            var result = await response.Content.ReadFromJsonAsync<RelatedStoriesResult>(JsonOptions, cancellationToken);
            return result ?? new RelatedStoriesResult();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"❌ [NewsApiClient] Failed to fetch related stories: {ex.Message}");
            return new RelatedStoriesResult();
        }
    }

    public async Task<List<BriefingItem>> GetArticlesAsync(
        string? category = null,
        string? search = null,
        string? contentType = null,
        int? days = null,
        int page = 1,
        int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var builder = new UriBuilder(new Uri(new Uri(normalizedBaseUrl), "api/v1/articles"));
            var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query["category"] = category;
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                query["search"] = search;
            }
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                query["contentType"] = contentType;
            }
            if (days.HasValue && days.Value > 0)
            {
                query["days"] = days.Value.ToString();
            }
            query["page"] = page.ToString();
            query["pageSize"] = pageSize.ToString();
            builder.Query = query.ToString();

            var response = await _httpClient.GetAsync(builder.Uri, cancellationToken);
            if (!response.IsSuccessStatusCode) return new List<BriefingItem>();

            var articles = await response.Content.ReadFromJsonAsync<List<BriefingItem>>(JsonOptions, cancellationToken);
            return articles ?? new List<BriefingItem>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"❌ [NewsApiClient] Failed to fetch articles: {ex.Message}");
            return new List<BriefingItem>();
        }
    }

    /// <summary>
    /// Fetches dedicated editorial and opinion pieces from major Nigerian newspaper outlets.
    /// </summary>
    public async Task<List<BriefingItem>> GetOpinionsAsync(
        string? search = null,
        int? days = 14,
        int page = 1,
        int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        return await GetArticlesAsync(
            category: null,
            search: search,
            contentType: "Opinion",
            days: days,
            page: page,
            pageSize: pageSize,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Fetches a single article by ID with full long-form content, summary, and metadata.
    /// </summary>
    public async Task<BriefingItem?> GetArticleByIdAsync(string articleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return null;

        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var uri = new Uri(new Uri(normalizedBaseUrl), $"api/v1/articles/{articleId}");
            var response = await _httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<BriefingItem>(JsonOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"❌ [NewsApiClient] Failed to fetch article by ID '{articleId}': {ex.Message}");
            return null;
        }
    }

    public async Task<bool> TrackArticleImpressionAsync(string articleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return false;
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var uri = new Uri(new Uri(normalizedBaseUrl), $"api/v1/articles/{articleId}/track-impression");
            var response = await _httpClient.PostAsync(uri, null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> TrackArticleClickAsync(string articleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(articleId)) return false;
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var uri = new Uri(new Uri(normalizedBaseUrl), $"api/v1/articles/{articleId}/track-click");
            var response = await _httpClient.PostAsync(uri, null, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Submits user feedback directly to the backend API.
    /// Stores entry in database and dispatches HTML email notification.
    /// </summary>
    public async Task<FeedbackResponseDto> SubmitFeedbackAsync(FeedbackRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var uri = new Uri(new Uri(normalizedBaseUrl), "api/v1/feedback");
            var response = await _httpClient.PostAsJsonAsync(uri, request, JsonOptions, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FeedbackResponseDto>(JsonOptions, cancellationToken);
                return result ?? new FeedbackResponseDto(true, "Thank you for your feedback!");
            }

            var err = await response.Content.ReadFromJsonAsync<FeedbackResponseDto>(JsonOptions, cancellationToken);
            return err ?? new FeedbackResponseDto(false, "Failed to submit feedback. Please try again.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new FeedbackResponseDto(false, $"Error submitting feedback: {ex.Message}");
        }
    }

    /// <summary>
    /// Fetches lightweight article delta records (e.g. category reclassifications) modified since the given UTC timestamp.
    /// Used by client-side SQLite cache to reconcile discrepancies with the central database.
    /// </summary>
    public async Task<List<ArticleDeltaDto>> GetArticleDeltasAsync(DateTime? sinceUtc = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(BaseUrl);
            var builder = new UriBuilder(new Uri(new Uri(normalizedBaseUrl), "api/v1/articles/sync"));
            if (sinceUtc.HasValue)
            {
                var query = System.Web.HttpUtility.ParseQueryString(builder.Query);
                query["sinceUtc"] = sinceUtc.Value.ToString("O");
                builder.Query = query.ToString();
            }

            var response = await _httpClient.GetAsync(builder.Uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new List<ArticleDeltaDto>();
            }

            var deltas = await response.Content.ReadFromJsonAsync<List<ArticleDeltaDto>>(JsonOptions, cancellationToken);
            return deltas ?? new List<ArticleDeltaDto>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[NewsApiClient] Delta sync error: {ex.Message}");
            return new List<ArticleDeltaDto>();
        }
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        return string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:56193" : baseUrl.Trim().TrimEnd('/');
    }
}

