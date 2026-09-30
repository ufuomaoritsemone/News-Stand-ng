using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AdminDashboard.Pages;

[Authorize]
public class CategorizerModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CategorizerModel> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CategorizerStatusViewModel Status { get; set; } = new();
    public List<CategorizerCorrectionViewModel> RecentCorrections { get; set; } = [];
    public int TotalCorrections { get; set; }

    public CategorizerModel(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<CategorizerModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration      = configuration;
        _logger             = logger;
    }

    public async Task OnGetAsync()
    {
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostRetrainAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var response = await client.PostAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/categorizer/retrain", null);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<TrainResultViewModel>(JsonOptions);
                var successMsg = result?.Message ?? "ML Categorizer model re-trained successfully!";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = true, message = successMsg, result });
                }

                TempData["Message"] = successMsg;
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                var errorMsg = $"Re-training request failed ({response.StatusCode}): {errorBody}";

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = false, message = errorMsg });
                }

                TempData["Error"] = errorMsg;
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error triggering model re-training: {ex.Message}";
            _logger.LogError(ex, "Error triggering categorizer re-training.");

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new { success = false, message = errorMsg });
            }

            TempData["Error"] = errorMsg;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPredictAsync([FromBody] PredictRequestViewModel request)
    {
        if (string.IsNullOrWhiteSpace(request?.Title))
        {
            return new JsonResult(new { success = false, message = "Please enter a headline to test." });
        }

        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            var response = await client.PostAsJsonAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/categorizer/predict", new
            {
                title = request.Title,
                summary = request.Summary
            });

            if (response.IsSuccessStatusCode)
            {
                var prediction = await response.Content.ReadFromJsonAsync<PredictionResponseViewModel>(JsonOptions);
                return new JsonResult(new { success = true, prediction });
            }

            var err = await response.Content.ReadAsStringAsync();
            return new JsonResult(new { success = false, message = $"Prediction error ({response.StatusCode}): {err}" });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = $"Error evaluating prediction: {ex.Message}" });
        }
    }

    private async Task LoadDataAsync()
    {
        var client = _httpClientFactory.CreateClient("NewsApiClient");
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "http://localhost:56193";

        try
        {
            // 1. Fetch ML Status
            var statusResp = await client.GetAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/categorizer/status");
            if (statusResp.IsSuccessStatusCode)
            {
                var s = await statusResp.Content.ReadFromJsonAsync<CategorizerStatusViewModel>(JsonOptions);
                if (s != null) Status = s;
            }

            // 2. Fetch Recent Corrections
            var correctionsResp = await client.GetAsync($"{apiBaseUrl.TrimEnd('/')}/api/v1/categorizer/corrections?limit=50");
            if (correctionsResp.IsSuccessStatusCode)
            {
                var body = await correctionsResp.Content.ReadFromJsonAsync<CorrectionsListResponseViewModel>(JsonOptions);
                if (body != null)
                {
                    RecentCorrections = body.Items ?? [];
                    TotalCorrections = body.Total;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load categorizer data for Admin Dashboard.");
            TempData["Error"] = $"Could not connect to NewsApi at {apiBaseUrl}: {ex.Message}";
        }
    }
}

public class CategorizerStatusViewModel
{
    public bool IsOperational { get; set; } = true;
    public bool IsTraining { get; set; }
    public DateTime? LastTrainedAt { get; set; }
    public double MicroAccuracy { get; set; } = 0.7283;
    public double MacroAccuracy { get; set; } = 0.5728;
    public double LogLoss { get; set; } = 0.8250;
    public double LogLossReduction { get; set; } = 0.5128;
    public int TotalTrainingSamples { get; set; }
    public int TotalCorrections { get; set; }
    public int CorrectionsSinceLastTraining { get; set; }
    public int AutoRetrainThreshold { get; set; } = 20;
    public string ModelPath { get; set; } = string.Empty;
}

public class CategorizerCorrectionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string ArticleId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string OldCategory { get; set; } = string.Empty;
    public string NewCategory { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? Url { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CorrectionsListResponseViewModel
{
    public int Total { get; set; }
    public List<CategorizerCorrectionViewModel> Items { get; set; } = [];
}

public class TrainResultViewModel
{
    public bool Success { get; set; }
    public double MicroAccuracy { get; set; }
    public double MacroAccuracy { get; set; }
    public double LogLoss { get; set; }
    public double LogLossReduction { get; set; }
    public int TotalTrainingSamples { get; set; }
    public int HumanCorrectionsUsed { get; set; }
    public DateTime TrainedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class PredictRequestViewModel
{
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
}

public class PredictionResponseViewModel
{
    public string PredictedCategory { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public Dictionary<string, float> Scores { get; set; } = [];
}
