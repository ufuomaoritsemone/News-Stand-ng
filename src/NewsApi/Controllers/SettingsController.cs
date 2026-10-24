using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NewsApi.Models;
using NewsApi.Services;
using Google.GenAI;

namespace NewsApi.Controllers;

[ApiController]
[Route("api/v1/settings")]
public class SettingsController(
    ISystemSettingsService settingsService,
    IHttpClientFactory httpClientFactory,
    ILogger<SettingsController> logger) : ControllerBase
{
    private readonly ISystemSettingsService _settingsService = settingsService;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<SettingsController> _logger = logger;

    /// <summary>
    /// Retrieves system diagnostic metrics and dynamic configuration settings.
    /// Secrets are safely masked (e.g. AIza••••••••).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSettings(CancellationToken ct = default)
    {
        var response = await _settingsService.GetSettingsWithDiagnosticsAsync(ct);
        return Ok(response);
    }

    /// <summary>
    /// Updates one or more system settings in the database.
    /// Masked values (starting with •••) are automatically skipped to prevent clobbering existing secrets.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequestDto request, CancellationToken ct = default)
    {
        if (request?.Settings == null || request.Settings.Count == 0)
        {
            return BadRequest(new { error = "No settings provided for update." });
        }

        await _settingsService.UpdateSettingsBatchAsync(request.Settings, ct);
        var updated = await _settingsService.GetSettingsWithDiagnosticsAsync(ct);
        return Ok(new
        {
            success = true,
            message = "System settings updated successfully.",
            data = updated
        });
    }

    /// <summary>
    /// Tests connectivity with the Google Gemini API using the provided or saved API key.
    /// </summary>
    [HttpPost("test-gemini")]
    public async Task<IActionResult> TestGemini([FromBody] TestConnectionRequestDto request, CancellationToken ct = default)
    {
        var apiKey = request?.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("•••"))
        {
            apiKey = await _settingsService.GetSettingAsync("Gemini:ApiKey", ct: ct);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return BadRequest(new TestConnectionResponseDto(false, "No Gemini API key is configured or provided."));
        }

        try
        {
            var client = new Client(apiKey: apiKey);
            var response = await client.Models.EmbedContentAsync(
                model: "gemini-embedding-001",
                contents: "News Stand NG connectivity test ping.",
                cancellationToken: ct
            );

            var model = await _settingsService.GetSettingAsync("Gemini:Model", "gemini-3.1-flash-lite", ct) ?? "gemini-3.1-flash-lite";
            var genResponse = await client.Models.GenerateContentAsync(
                model: model,
                contents: "Respond in 3 words: Ready",
                cancellationToken: ct
            );
            var genText = genResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text?.Trim() ?? "OK";

            var first = response?.Embeddings?.FirstOrDefault();
            var dimText = first?.Values != null && first.Values.Count > 0 ? $"{first.Values.Count}-dim vector embedding" : "embedding";
            return Ok(new TestConnectionResponseDto(true, $"Successfully connected to Google Gemini! Generated {dimText} & verified model '{model}' (\"{genText}\")."));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini API test probe failed");
            return Ok(new TestConnectionResponseDto(false, $"Gemini connection test failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Tests connectivity with the YouTube Data API v3 using the provided or saved API key.
    /// </summary>
    [HttpPost("test-youtube")]
    public async Task<IActionResult> TestYouTube([FromBody] TestConnectionRequestDto request, CancellationToken ct = default)
    {
        var apiKey = request?.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("•••"))
        {
            apiKey = await _settingsService.GetSettingAsync("YouTube:ApiKey", ct: ct);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return BadRequest(new TestConnectionResponseDto(false, "No YouTube API key is configured or provided."));
        }

        try
        {
            var http = _httpClientFactory.CreateClient();
            var testUrl = $"https://www.googleapis.com/youtube/v3/videos?part=snippet&chart=mostPopular&regionCode=NG&videoCategoryId=25&maxResults=1&key={Uri.EscapeDataString(apiKey)}";
            var response = await http.GetAsync(testUrl, ct);

            if (response.IsSuccessStatusCode)
            {
                return Ok(new TestConnectionResponseDto(true, "Successfully connected to YouTube Data API v3! Nigerian news video quota verified.", (int)response.StatusCode));
            }

            var errBody = await response.Content.ReadAsStringAsync(ct);
            return Ok(new TestConnectionResponseDto(false, $"YouTube API returned HTTP {(int)response.StatusCode}: {errBody}", (int)response.StatusCode));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "YouTube API test probe failed");
            return Ok(new TestConnectionResponseDto(false, $"YouTube API test failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Tests the SMTP email configuration by attempting to send a test message.
    /// </summary>
    [HttpPost("test-smtp")]
    public async Task<IActionResult> TestSmtp([FromBody] TestConnectionRequestDto request, CancellationToken ct = default)
    {
        var targetEmail = !string.IsNullOrWhiteSpace(request?.Target)
            ? request.Target.Trim()
            : await _settingsService.GetSettingAsync("Feedback:RecipientEmail", "feedback@nigeriannewsgrid.com", ct);

        var host = await _settingsService.GetSettingAsync("Feedback:Smtp:Host", ct: ct);
        if (string.IsNullOrWhiteSpace(host))
        {
            return BadRequest(new TestConnectionResponseDto(false, "SMTP Host is not configured."));
        }

        var portStr = await _settingsService.GetSettingAsync("Feedback:Smtp:Port", "587", ct);
        var port = int.TryParse(portStr, out var p) ? p : 587;
        var enableSsl = await _settingsService.GetBoolSettingAsync("Feedback:Smtp:EnableSsl", true, ct);
        var username = await _settingsService.GetSettingAsync("Feedback:Smtp:UserName", ct: ct);
        var password = await _settingsService.GetSettingAsync("Feedback:Smtp:Password", ct: ct);
        var fromEmail = await _settingsService.GetSettingAsync("Feedback:Smtp:FromEmail", "no-reply@nigeriannewsgrid.com", ct) ?? "no-reply@nigeriannewsgrid.com";
        var fromName = await _settingsService.GetSettingAsync("Feedback:Smtp:FromName", "News Stand NG", ct) ?? "News Stand NG";

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                Timeout = 10000
            };

            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
            {
                client.Credentials = new NetworkCredential(username, password);
            }

            using var msg = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = "News Stand NG — SMTP Diagnostic Verification",
                Body = $"This is a test notification confirming that SMTP email delivery is functioning correctly on News Stand NG at {DateTime.UtcNow:u}.",
                IsBodyHtml = false
            };
            msg.To.Add(targetEmail!);

            await client.SendMailAsync(msg, ct);
            return Ok(new TestConnectionResponseDto(true, $"Test email dispatched successfully to {targetEmail}!"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP test probe failed");
            return Ok(new TestConnectionResponseDto(false, $"SMTP dispatch failed: {ex.Message}"));
        }
    }
}
