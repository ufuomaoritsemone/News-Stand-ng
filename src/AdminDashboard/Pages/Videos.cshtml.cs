using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace AdminDashboard.Pages;

public class VideosModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public VideosModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [BindProperty]
    public VideoChannelInput NewChannel { get; set; } = new();

    public List<VideoChannelDto> Channels { get; set; } = new();
    public List<VideoStoryDto> Stories { get; set; } = new();
    public List<VideoStoryDto> TrendingStories { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostAddChannelAsync()
    {
        if (string.IsNullOrWhiteSpace(NewChannel.ChannelName))
        {
            TempData["Error"] = "Channel name is required.";
            await LoadDataAsync();
            return Page();
        }

        var client = _httpClientFactory.CreateClient();
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var response = await client.PostAsJsonAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/video-channels", new
            {
                NewChannel.ChannelName,
                NewChannel.YoutubeChannelId,
                NewChannel.ThumbnailUrl,
                NewChannel.ChannelUrl,
                NewChannel.Description
            });

            if (response.IsSuccessStatusCode)
                TempData["Message"] = $"YouTube channel '{NewChannel.ChannelName}' added & latest videos synced!";
            else
                TempData["Error"] = "Failed to add video channel.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error connecting to API: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSyncVideosAsync()
    {
        var client = _httpClientFactory.CreateClient();
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var response = await client.PostAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/video-stories/sync", null);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SyncResultDto>();
                TempData["Message"] = result?.Message ?? "YouTube feeds synced successfully!";
            }
            else
            {
                TempData["Error"] = "Failed to sync YouTube feeds.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSyncTrendingAsync()
    {
        var client = _httpClientFactory.CreateClient();
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var response = await client.PostAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/video-stories/trending/sync", null);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SyncResultDto>();
                TempData["Message"] = result?.Message ?? "Trending YouTube stories synced successfully!";
            }
            else
            {
                TempData["Error"] = "Failed to sync trending YouTube stories.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveChannelAsync(string id)
    {
        var client = _httpClientFactory.CreateClient();
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var response = await client.DeleteAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/video-channels/{id}");
            if (response.IsSuccessStatusCode)
                TempData["Message"] = "Video channel removed.";
            else
                TempData["Error"] = "Failed to remove video channel.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveStoryAsync(string id)
    {
        var client = _httpClientFactory.CreateClient();
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var response = await client.DeleteAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/video-stories/{id}");
            if (response.IsSuccessStatusCode)
                TempData["Message"] = "Video story removed.";
            else
                TempData["Error"] = "Failed to remove video story.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error: {ex.Message}";
        }

        return RedirectToPage();
    }

    private async Task LoadDataAsync()
    {
        var client = _httpClientFactory.CreateClient();
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var chTask = client.GetAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/video-channels");
            var stTask = client.GetAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/video-stories");
            var trTask = client.GetAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/video-stories/trending");

            await Task.WhenAll(chTask, stTask, trTask);

            var chResp = await chTask;
            if (chResp.IsSuccessStatusCode)
            {
                Channels = (await chResp.Content.ReadFromJsonAsync<List<VideoChannelDto>>()) ?? new();
            }

            var stResp = await stTask;
            if (stResp.IsSuccessStatusCode)
            {
                Stories = (await stResp.Content.ReadFromJsonAsync<List<VideoStoryDto>>()) ?? new();
            }

            var trResp = await trTask;
            if (trResp.IsSuccessStatusCode)
            {
                TrendingStories = (await trResp.Content.ReadFromJsonAsync<List<VideoStoryDto>>()) ?? new();
            }
        }
        catch
        {
            Channels = new();
            Stories = new();
            TrendingStories = new();
        }
    }
}

public class VideoChannelInput
{
    public string ChannelName { get; set; } = string.Empty;
    public string? YoutubeChannelId { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? ChannelUrl { get; set; }
    public string? Description { get; set; }
}

public class VideoChannelDto
{
    public string Id { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string YoutubeChannelId { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string ChannelUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class VideoStoryDto
{
    public string Id { get; set; } = string.Empty;
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string Duration { get; set; } = "12:00";
    public string Category { get; set; } = "News";
    public DateTime PublishedAt { get; set; }
    public bool IsTrending { get; set; }
    public int? TrendingRank { get; set; }
    public long ViewCount { get; set; }
    public long LikeCount { get; set; }
}

public class SyncResultDto
{
    public string? Message { get; set; }
    public int NewCount { get; set; }
    public int UpdatedCount { get; set; }
}
