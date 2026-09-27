using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace AdminDashboard.Pages;

public class SocialsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public SocialsModel(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [BindProperty]
    public SocialHandleInput NewHandle { get; set; } = new();

    public List<SocialHandleDto> Handles { get; set; } = new();
    public List<SocialPostDto> Posts { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostAddHandleAsync()
    {
        if (string.IsNullOrWhiteSpace(NewHandle.Handle))
        {
            TempData["Error"] = "Twitter/X handle is required.";
            await LoadDataAsync();
            return Page();
        }

        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var response = await client.PostAsJsonAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/social-handles", new
            {
                NewHandle.Handle,
                NewHandle.DisplayName,
                NewHandle.ProfileUrl,
                NewHandle.AvatarUrl,
                NewHandle.Bio,
                NewHandle.Category
            });

            if (response.IsSuccessStatusCode)
                TempData["Message"] = $"Social handle '{NewHandle.Handle}' added & latest tweets synced!";
            else
                TempData["Error"] = "Failed to add social handle.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error connecting to API: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSyncSocialsAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var response = await client.PostAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/social-posts/sync", null);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SyncResultDto>();
                TempData["Message"] = result?.Message ?? "Social stories synced successfully!";
            }
            else
            {
                TempData["Error"] = "Failed to sync social stories.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveHandleAsync(string id)
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var response = await client.DeleteAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/social-handles/{id}");
            if (response.IsSuccessStatusCode)
                TempData["Message"] = "Social handle removed.";
            else
                TempData["Error"] = "Failed to remove social handle.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemovePostAsync(string id)
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var response = await client.DeleteAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/social-posts/{id}");
            if (response.IsSuccessStatusCode)
                TempData["Message"] = "Social post removed.";
            else
                TempData["Error"] = "Failed to remove post.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error: {ex.Message}";
        }

        return RedirectToPage();
    }

    private async Task LoadDataAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";
        try
        {
            var hTask = client.GetAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/social-handles");
            var pTask = client.GetAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/social-posts");

            await Task.WhenAll(hTask, pTask);

            var hResp = await hTask;
            if (hResp.IsSuccessStatusCode)
            {
                Handles = (await hResp.Content.ReadFromJsonAsync<List<SocialHandleDto>>()) ?? new();
            }

            var pResp = await pTask;
            if (pResp.IsSuccessStatusCode)
            {
                Posts = (await pResp.Content.ReadFromJsonAsync<List<SocialPostDto>>()) ?? new();
            }
        }
        catch
        {
            Handles = new();
            Posts = new();
        }
    }
}

public class SocialHandleInput
{
    public string Handle { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? ProfileUrl { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? Category { get; set; }
}

public class SocialHandleDto
{
    public string Id { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ProfileUrl { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string Category { get; set; } = "Media";
    public DateTime CreatedAt { get; set; }
}

public class SocialPostDto
{
    public string Id { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorHandle { get; set; } = string.Empty;
    public string AuthorAvatarUrl { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string PostUrl { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string Category { get; set; } = "Breaking";
    public int LikesCount { get; set; }
    public int RetweetsCount { get; set; }
    public DateTime PublishedAt { get; set; }
}
