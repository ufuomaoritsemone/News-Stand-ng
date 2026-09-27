namespace TtsWorker.Synthesizers;

/// <summary>
/// Result container returned by any TTS synthesis provider in the Tri-Tier architecture.
/// Encapsulates raw audio bytes, MIME content type, identifying provider name, and error diagnostic details.
/// </summary>
/// <param name="Success">Indicates whether synthesis was successful.</param>
/// <param name="AudioData">The raw encoded audio bytes (MP3 or WAV), or null on failure.</param>
/// <param name="ContentType">MIME type of the audio stream (e.g., "audio/mpeg", "audio/wav").</param>
/// <param name="ProviderUsed">Name of the provider that performed the synthesis.</param>
/// <param name="ErrorMessage">Diagnostic error details if synthesis failed.</param>
public sealed record TtsSynthesisResult(
    bool Success,
    byte[]? AudioData,
    string ContentType,
    string ProviderUsed,
    string? ErrorMessage = null
);

/// <summary>
/// Pluggable contract for speech synthesis engines in the Nigerian News Grid Tri-Tier architecture:
/// 1. GoogleCloudTtsSynthesizer: High-fidelity Neural2 provider, activated when GCP_TTS_API_KEY is configured.
/// 2. EdgeNeuralTtsSynthesizer: Primary zero-cost neural engine using authentic Nigerian English voices (en-NG).
/// 3. LocalFallbackTtsSynthesizer: Offline resilient safety net ensuring jobs never crash during network outages.
/// </summary>
public interface ITtsSynthesizer
{
    /// <summary>
    /// Friendly display identifier for logging, telemetry, and audio registration.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Indicates whether the provider has all necessary configuration (e.g. API keys, dependencies) to execute.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Synthesizes news text or briefings into audio bytes.
    /// </summary>
    /// <param name="text">The plain text news summary or headline to speak.</param>
    /// <param name="voiceOrLanguage">Optional specific voice identifier (e.g., "en-NG-EzinneNeural" or "en-NG-Neural2-A").</param>
    /// <param name="cancellationToken">Cancellation token to abort in-flight synthesis.</param>
    /// <returns>A TtsSynthesisResult indicating success status and audio payload.</returns>
    Task<TtsSynthesisResult> SynthesizeAsync(string text, string? voiceOrLanguage = null, CancellationToken cancellationToken = default);
}
