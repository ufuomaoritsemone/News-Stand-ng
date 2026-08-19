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

    private static string NormalizeBaseUrl(string baseUrl)
    {
        return string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:56193" : baseUrl.Trim().TrimEnd('/');
    }
}
