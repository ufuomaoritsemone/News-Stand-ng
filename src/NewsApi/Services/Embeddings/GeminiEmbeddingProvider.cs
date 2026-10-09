using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsApi.Services.Embeddings;

public class GeminiEmbeddingProvider : IEmbeddingProvider
{
    public const string DefaultModel = "gemini-embedding-001";
    public int Dimensions => 768;

    private readonly IConfiguration _config;
    private readonly LocalFallbackEmbeddingProvider _fallback;
    private readonly ILogger<GeminiEmbeddingProvider> _logger;
    private readonly IServiceScopeFactory? _scopeFactory;
    private Client? _client;
    private string? _cachedApiKey;
    private readonly object _clientLock = new();

    public GeminiEmbeddingProvider(
        IConfiguration config,
        LocalFallbackEmbeddingProvider fallback,
        ILogger<GeminiEmbeddingProvider> logger,
        IHttpClientFactory? httpClientFactory = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        _config = config;
        _fallback = fallback;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[Dimensions];
        }

        var client = GetClient();
        if (client == null)
        {
            return await _fallback.GenerateEmbeddingAsync(text, cancellationToken);
        }

        try
        {
            var response = await client.Models.EmbedContentAsync(
                model: DefaultModel,
                contents: text,
                config: new EmbedContentConfig
                {
                    OutputDimensionality = Dimensions
                },
                cancellationToken: cancellationToken);

            var first = response?.Embeddings?.FirstOrDefault();
            if (first?.Values != null && first.Values.Count > 0)
            {
                var vec = first.Values.Select(v => (float)v).ToArray();
                VectorMath.Normalize(vec);
                return vec;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Gemini embedding request via Google.GenAI failed. Falling back to local provider.");
        }

        return await _fallback.GenerateEmbeddingAsync(text, cancellationToken);
    }

    public async Task<List<float[]>> GenerateEmbeddingsBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        if (textList.Count == 0) return [];

        var client = GetClient();
        if (client == null)
        {
            return await _fallback.GenerateEmbeddingsBatchAsync(textList, cancellationToken);
        }

        try
        {
            var results = new float[textList.Count][];
            using var semaphore = new SemaphoreSlim(5); // Bound concurrent calls to stay within API rate limits

            var tasks = textList.Select(async (text, i) =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    results[i] = await GenerateEmbeddingAsync(text, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return results.ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Gemini batch embedding request failed. Falling back to local provider.");
            return await _fallback.GenerateEmbeddingsBatchAsync(textList, cancellationToken);
        }
    }

    private Client? GetClient()
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        lock (_clientLock)
        {
            if (_client == null || _cachedApiKey != apiKey)
            {
                _client = new Client(apiKey: apiKey);
                _cachedApiKey = apiKey;
            }
        }

        return _client;
    }

    private string? GetApiKey()
    {
        if (_scopeFactory != null)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var settings = scope.ServiceProvider.GetService<ISystemSettingsService>();
                if (settings != null)
                {
                    var dyn = settings.GetSettingAsync("Gemini:ApiKey").GetAwaiter().GetResult();
                    if (!string.IsNullOrWhiteSpace(dyn)) return dyn;
                }
            }
            catch { }
        }

        return _config["Gemini:ApiKey"]
            ?? _config["GoogleCloud:ApiKey"]
            ?? _config["Embeddings:ApiKey"]
            ?? _config["ApiKey"]
            ?? System.Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    }
}
