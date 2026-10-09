using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsApi.Services.Embeddings;

public class GeminiEmbeddingProvider : IEmbeddingProvider
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LocalFallbackEmbeddingProvider _fallback;
    private readonly ILogger<GeminiEmbeddingProvider> _logger;

    public int Dimensions => 768;

    public GeminiEmbeddingProvider(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        LocalFallbackEmbeddingProvider fallback,
        ILogger<GeminiEmbeddingProvider> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[Dimensions];
        }

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return await _fallback.GenerateEmbeddingAsync(text, cancellationToken);
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/text-embedding-004:embedContent?key={apiKey}";
            var requestBody = new
            {
                model = "models/text-embedding-004",
                content = new
                {
                    parts = new[] { new { text } }
                }
            };

            var response = await client.PostAsJsonAsync(url, requestBody, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini embedding endpoint returned HTTP {StatusCode}. Using local fallback.", response.StatusCode);
                return await _fallback.GenerateEmbeddingAsync(text, cancellationToken);
            }

            var result = await response.Content.ReadFromJsonAsync<GeminiEmbedContentResponse>(cancellationToken);
            if (result?.Embedding?.Values != null && result.Embedding.Values.Length > 0)
            {
                var vec = result.Embedding.Values;
                VectorMath.Normalize(vec);
                return vec;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Gemini embedding request failed. Falling back to local provider.");
        }

        return await _fallback.GenerateEmbeddingAsync(text, cancellationToken);
    }

    public async Task<List<float[]>> GenerateEmbeddingsBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        if (textList.Count == 0) return [];

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return await _fallback.GenerateEmbeddingsBatchAsync(textList, cancellationToken);
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/text-embedding-004:batchEmbedContents?key={apiKey}";
            var requests = textList.Select(t => new
            {
                model = "models/text-embedding-004",
                content = new { parts = new[] { new { text = t } } }
            }).ToList();

            var requestBody = new { requests };
            var response = await client.PostAsJsonAsync(url, requestBody, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GeminiBatchEmbedResponse>(cancellationToken);
                if (result?.Embeddings != null && result.Embeddings.Count == textList.Count)
                {
                    var results = new List<float[]>();
                    foreach (var emb in result.Embeddings)
                    {
                        var vec = emb.Values ?? new float[Dimensions];
                        VectorMath.Normalize(vec);
                        results.Add(vec);
                    }
                    return results;
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Gemini batch embedding request failed. Falling back to local provider.");
        }

        return await _fallback.GenerateEmbeddingsBatchAsync(textList, cancellationToken);
    }

    private string? GetApiKey()
    {
        return _config["Gemini:ApiKey"] ?? _config["GoogleCloud:ApiKey"] ?? _config["Embeddings:ApiKey"];
    }

    private sealed class GeminiEmbedContentResponse
    {
        [JsonPropertyName("embedding")]
        public ContentEmbedding? Embedding { get; set; }
    }

    private sealed class GeminiBatchEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<ContentEmbedding>? Embeddings { get; set; }
    }

    private sealed class ContentEmbedding
    {
        [JsonPropertyName("values")]
        public float[]? Values { get; set; }
    }
}
