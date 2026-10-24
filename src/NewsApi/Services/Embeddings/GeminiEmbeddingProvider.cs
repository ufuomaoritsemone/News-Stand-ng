using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewsApi.Services;

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

    // In-memory circuit breaker for Gemini embeddings: if calls fail, fallback to LocalFallbackEmbeddingProvider
    // without making remote calls until cooldown expires.
    private static DateTime _circuitBreakerUntilUtc = DateTime.MinValue;
    private static readonly TimeSpan CircuitBreakerCooldown = TimeSpan.FromMinutes(10);
    private static readonly object CircuitLock = new();

    public static bool IsCircuitBreakerActive
    {
        get
        {
            lock (CircuitLock)
            {
                return DateTime.UtcNow < _circuitBreakerUntilUtc;
            }
        }
    }

    public static void TripCircuitBreaker(string reason, ILogger? logger = null)
    {
        lock (CircuitLock)
        {
            _circuitBreakerUntilUtc = DateTime.UtcNow.Add(CircuitBreakerCooldown);
        }
        logger?.LogWarning(
            "\n╔══════════════════════════════════════════════════════════════════════════════╗\n" +
            "║ ⚡ [GEMINI EMBEDDING CIRCUIT BREAKER TRIPPED] Fallback to Local Embedding      ║\n" +
            "║ Reason  : {Reason}\n" +
            "║ Cooldown: Active until {CooldownUntilUtc:u} (10 min bypass)                  ║\n" +
            "╚══════════════════════════════════════════════════════════════════════════════╝",
            reason, _circuitBreakerUntilUtc);
    }

    public static void ResetCircuitBreaker()
    {
        lock (CircuitLock)
        {
            _circuitBreakerUntilUtc = DateTime.MinValue;
        }
    }

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

        if (IsCircuitBreakerActive)
        {
            return await _fallback.GenerateEmbeddingAsync(text, cancellationToken);
        }

        var client = GetClient();
        if (client == null)
        {
            return await _fallback.GenerateEmbeddingAsync(text, cancellationToken);
        }

        try
        {
            _logger.LogInformation(
                "\n╔══════════════════════════════════════════════════════════════════════════════╗\n" +
                "║ 🧠 [GEMINI EMBEDDING ENGAGED] Model: {Model} | Dimensions: {Dims}             ║\n" +
                "╚══════════════════════════════════════════════════════════════════════════════╝",
                DefaultModel, Dimensions);

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
            TripCircuitBreaker(ex.Message, _logger);
            _logger.LogWarning(ex, "Gemini embedding request via Google.GenAI failed. Tripped circuit breaker. Falling back to local provider.");
        }

        return await _fallback.GenerateEmbeddingAsync(text, cancellationToken);
    }

    public async Task<List<float[]>> GenerateEmbeddingsBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        if (textList.Count == 0) return [];

        if (IsCircuitBreakerActive)
        {
            return await _fallback.GenerateEmbeddingsBatchAsync(textList, cancellationToken);
        }

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
            TripCircuitBreaker(ex.Message, _logger);
            _logger.LogWarning(ex, "Gemini batch embedding request failed. Tripped circuit breaker. Falling back to local provider.");
            return await _fallback.GenerateEmbeddingsBatchAsync(textList, cancellationToken);
        }
    }

    private Client? GetClient()
    {
        var apiKey = GetApiKey();
        var adcEnv = System.Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
        if (string.IsNullOrWhiteSpace(adcEnv) || !System.IO.File.Exists(adcEnv))
        {
            var userGcloudAdc = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "gcloud", "application_default_credentials.json");
            if (System.IO.File.Exists(userGcloudAdc))
            {
                System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", userGcloudAdc);
                adcEnv = userGcloudAdc;
            }
            else
            {
                var repoSecrets = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "secrets", "gcp-service-account.json"));
                if (System.IO.File.Exists(repoSecrets))
                {
                    System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", repoSecrets);
                    adcEnv = repoSecrets;
                }
            }
        }

        var hasAdc = !string.IsNullOrWhiteSpace(adcEnv)
            || !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("GOOGLE_GENAI_USE_VERTEXAI"))
            || System.IO.File.Exists(System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "gcloud", "application_default_credentials.json"));

        if (string.IsNullOrWhiteSpace(apiKey) && !hasAdc) return null;

        lock (_clientLock)
        {
            if (_client == null || _cachedApiKey != apiKey)
            {
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    // Ensure Google AI Studio API key routes to generativelanguage.googleapis.com, not Vertex AI
                    System.Environment.SetEnvironmentVariable("GOOGLE_GENAI_USE_VERTEXAI", null);
                    _client = new Client(apiKey: apiKey);
                }
                else
                {
                    _client = new Client();
                }
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

        return System.Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? _config["Gemini:ApiKey"]
            ?? _config["Gemini__ApiKey"]
            ?? _config["GoogleCloud:ApiKey"]
            ?? _config["Embeddings:ApiKey"]
            ?? _config["ApiKey"]
            ?? System.Environment.GetEnvironmentVariable("API_KEY");
    }
}
