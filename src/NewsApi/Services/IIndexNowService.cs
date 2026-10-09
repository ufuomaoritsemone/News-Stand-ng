namespace NewsApi.Services;

/// <summary>
/// Service for submitting newly published or modified URLs to search engines via the IndexNow protocol.
/// Submissions are queued in a non-blocking background channel and dispatched in batches with retry and jitter.
/// </summary>
public interface IIndexNowService
{
    /// <summary>
    /// Enqueues a URL for asynchronous submission to IndexNow without blocking caller ingestion.
    /// </summary>
    ValueTask EnqueueUrlAsync(string url, CancellationToken ct = default);

    /// <summary>
    /// Enqueues a collection of URLs for asynchronous batched submission to IndexNow without blocking caller ingestion.
    /// </summary>
    ValueTask EnqueueUrlsAsync(IEnumerable<string> urls, CancellationToken ct = default);

    /// <summary>
    /// Gets the configured IndexNow key, host, and key location.
    /// </summary>
    (string Key, string Host, string KeyLocation) GetConfiguration();
}
