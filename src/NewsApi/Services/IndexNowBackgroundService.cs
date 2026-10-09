using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NewsApi.Services;

public record IndexNowPayload
{
    [JsonPropertyName("host")]
    public string Host { get; init; } = "";

    [JsonPropertyName("key")]
    public string Key { get; init; } = "";

    [JsonPropertyName("keyLocation")]
    public string KeyLocation { get; init; } = "";

    [JsonPropertyName("urlList")]
    public List<string> UrlList { get; init; } = [];
}

public class IndexNowBackgroundService : BackgroundService, IIndexNowService
{
    private readonly Channel<string> _channel;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<IndexNowBackgroundService> _logger;

    public string Host { get; }
    public string Key { get; }
    public string KeyLocation { get; }
    public string Endpoint { get; }
    public bool Enabled { get; }

    public IndexNowBackgroundService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<IndexNowBackgroundService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;

        _httpClient.Timeout = TimeSpan.FromSeconds(5);

        Host = _config["IndexNow:Host"] ?? "newsstand.ng";
        Key = _config["IndexNow:Key"] ?? "3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d";
        KeyLocation = _config["IndexNow:KeyLocation"] ?? $"https://{Host}/{Key}.txt";
        Endpoint = _config["IndexNow:Endpoint"] ?? "https://api.indexnow.org/indexnow";
        Enabled = bool.TryParse(_config["IndexNow:Enabled"], out var enabled) ? enabled : true;

        var options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateUnbounded<string>(options);
    }

    public (string Key, string Host, string KeyLocation) GetConfiguration() => (Key, Host, KeyLocation);

    public ValueTask EnqueueUrlAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return ValueTask.CompletedTask;
        _channel.Writer.TryWrite(url.Trim());
        return ValueTask.CompletedTask;
    }

    public ValueTask EnqueueUrlsAsync(IEnumerable<string> urls, CancellationToken ct = default)
    {
        if (urls is null) return ValueTask.CompletedTask;
        foreach (var url in urls)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                _channel.Writer.TryWrite(url.Trim());
            }
        }
        return ValueTask.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IndexNow background dispatcher initialized (Enabled={Enabled}, Host={Host})", Enabled, Host);

        var batch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for at least one item to arrive
                if (!await _channel.Reader.WaitToReadAsync(stoppingToken))
                {
                    break;
                }

                // Debounce / collect up to 100 items within 2 seconds
                var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

                try
                {
                    while (batch.Count < 100 && _channel.Reader.TryRead(out var url))
                    {
                        batch.Add(url);
                    }

                    while (batch.Count < 100 && !timeoutCts.IsCancellationRequested)
                    {
                        if (await _channel.Reader.WaitToReadAsync(timeoutCts.Token))
                        {
                            while (batch.Count < 100 && _channel.Reader.TryRead(out var url))
                            {
                                batch.Add(url);
                            }
                        }
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Debounce window elapsed; proceed to process batch
                }

                if (batch.Count > 0)
                {
                    var urlList = batch.ToList();
                    batch.Clear();

                    if (Enabled && !string.IsNullOrWhiteSpace(Key))
                    {
                        await SubmitBatchWithRetryAsync(urlList, stoppingToken);
                    }
                    else
                    {
                        _logger.LogDebug("IndexNow disabled or missing key. Skipping submission of {Count} URLs.", urlList.Count);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in IndexNow background loop");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    public async Task<bool> SubmitBatchWithRetryAsync(List<string> urlList, CancellationToken ct = default, int maxRetries = 3)
    {
        if (urlList.Count == 0) return true;

        var payload = new IndexNowPayload
        {
            Host = Host,
            Key = Key,
            KeyLocation = KeyLocation,
            UrlList = urlList
        };

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var response = await _httpClient.PostAsJsonAsync(Endpoint, payload, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully submitted {Count} URLs to IndexNow (Attempt {Attempt})", urlList.Count, attempt);
                    return true;
                }

                var statusCode = (int)response.StatusCode;
                var isTransient = statusCode == 429 || statusCode >= 500;

                _logger.LogWarning("IndexNow submission returned status {StatusCode} (Attempt {Attempt}/{MaxRetries})", response.StatusCode, attempt, maxRetries);

                if (!isTransient || attempt == maxRetries)
                {
                    return false;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                _logger.LogWarning("IndexNow HTTP request failed on attempt {Attempt}/{MaxRetries}: {Message}", attempt, maxRetries, ex.Message);
                if (attempt == maxRetries || ct.IsCancellationRequested)
                {
                    return false;
                }
            }

            // Exponential backoff with jitter: 2^(attempt - 1) * 300ms + [50ms - 200ms] jitter
            var backoffMs = (int)(Math.Pow(2, attempt - 1) * 300) + Random.Shared.Next(50, 200);
            await Task.Delay(backoffMs, ct);
        }

        return false;
    }
}
