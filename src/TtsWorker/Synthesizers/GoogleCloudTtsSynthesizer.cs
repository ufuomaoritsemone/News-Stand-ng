using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TtsWorker.Synthesizers;

/// <summary>
/// Architecture Component: Tri-Tier TTS Architecture — Tier 1: Future Google Cloud Text-to-Speech Engine.
/// 
/// Purpose:
/// Provides native integration with Google Cloud Text-to-Speech Neural2 models (en-NG-Neural2-A).
/// 
/// Activation Model:
/// - By default, this provider remains dormant with zero overhead or configuration errors.
/// - When an API key is provided via configuration ("GoogleCloud:ApiKey" or "GCP_TTS_API_KEY" or environment variable),
///   IsConfigured evaluates to true, and CompositeTtsSynthesizer automatically elevates it to Priority Tier 1.
/// - If the API key is expired, invalid, or quota is exhausted, synthesis returns a graceful failure so the pipeline
///   seamlessly drops to Tier 2 (Microsoft Edge Neural TTS, Option B).
/// </summary>
public sealed class GoogleCloudTtsSynthesizer : ITtsSynthesizer
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleCloudTtsSynthesizer> _logger;

    private const string DefaultVoice = "en-NG-Neural2-A";
    private const string DefaultLanguageCode = "en-NG";

    public string ProviderName => "GoogleCloud";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(GetApiKey());

    public GoogleCloudTtsSynthesizer(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GoogleCloudTtsSynthesizer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TtsSynthesisResult> SynthesizeAsync(
        string text,
        string? voiceOrLanguage = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new TtsSynthesisResult(
                Success: false,
                AudioData: null,
                ContentType: "audio/mpeg",
                ProviderUsed: ProviderName,
                ErrorMessage: "Google Cloud TTS API Key is not configured."
            );
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new TtsSynthesisResult(false, null, "audio/mpeg", ProviderName, "Empty text provided for synthesis.");
        }

        try
        {
            var voiceName = !string.IsNullOrWhiteSpace(voiceOrLanguage) ? voiceOrLanguage : DefaultVoice;
            var languageCode = voiceName.Length >= 5 ? voiceName.Substring(0, 5) : DefaultLanguageCode;

            _logger.LogInformation("Calling Google Cloud Text-to-Speech API with voice '{Voice}'...", voiceName);

            var requestUri = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={Uri.EscapeDataString(apiKey)}";
            var requestBody = new
            {
                input = new { text },
                voice = new
                {
                    languageCode,
                    name = voiceName
                },
                audioConfig = new
                {
                    audioEncoding = "MP3",
                    speakingRate = 1.0,
                    pitch = 0.0
                }
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(25);

            var response = await client.PostAsJsonAsync(requestUri, requestBody, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Google Cloud TTS API returned HTTP {StatusCode}: {Error}", response.StatusCode, errorBody);
                return new TtsSynthesisResult(
                    Success: false,
                    AudioData: null,
                    ContentType: "audio/mpeg",
                    ProviderUsed: ProviderName,
                    ErrorMessage: $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"
                );
            }

            var gcpResponse = await response.Content.ReadFromJsonAsync<GcpTtsResponse>(cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(gcpResponse?.AudioContent))
            {
                _logger.LogWarning("Google Cloud TTS returned empty audioContent.");
                return new TtsSynthesisResult(false, null, "audio/mpeg", ProviderName, "Empty audioContent in Google Cloud response.");
            }

            var audioBytes = Convert.FromBase64String(gcpResponse.AudioContent);
            _logger.LogInformation("Successfully synthesized {Bytes} bytes via Google Cloud TTS ({Voice}).", audioBytes.Length, voiceName);

            return new TtsSynthesisResult(
                Success: true,
                AudioData: audioBytes,
                ContentType: "audio/mpeg",
                ProviderUsed: ProviderName
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Cloud TTS synthesis failed: {Error}. Seamless fallback will occur.", ex.Message);
            return new TtsSynthesisResult(false, null, "audio/mpeg", ProviderName, ex.Message);
        }
    }

    private string? GetApiKey()
    {
        return _configuration["GoogleCloud:ApiKey"]
            ?? _configuration["GCP_TTS_API_KEY"]
            ?? Environment.GetEnvironmentVariable("GCP_TTS_API_KEY")
            ?? _configuration["GoogleCloud__ApiKey"];
    }

    private sealed record GcpTtsResponse
    {
        [JsonPropertyName("audioContent")]
        public string? AudioContent { get; init; }
    }
}
