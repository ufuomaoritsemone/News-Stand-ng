using Microsoft.Extensions.Logging;

namespace TtsWorker.Synthesizers;

/// <summary>
/// Architecture Component: Tri-Tier TTS Architecture — Composite Orchestrator & Strategy Router.
/// 
/// Routing Priority:
/// 1. Google Cloud Text-to-Speech (Tier 1):
///    Invoked if an API key is provided in configuration ("GoogleCloud:ApiKey" or "GCP_TTS_API_KEY").
///    Uses Neural2 Nigerian English model ("en-NG-Neural2-A").
/// 
/// 2. Microsoft Edge Neural TTS (Tier 2, Option B — Primary Free Default):
///    Invoked when Google Cloud is not configured or fails.
///    Zero API keys required, $0 cloud cost.
///    Synthesizes authentic Nigerian English audio ("en-NG-EzinneNeural" / "en-NG-AbeoNeural")
///    over secure TLS WebSockets directly into standard MP3.
/// 
/// 3. Local Fallback Synthesizer (Tier 3 — Offline Safety Net):
///    Invoked if external network connections are unavailable or all remote calls fail.
///    Generates an immediate, valid, playable RIFF WAVE audio container with acoustic chime.
///    Ensures TtsWorker background services and Docker containers never crash or exit unexpectedly.
/// </summary>
public sealed class CompositeTtsSynthesizer : ITtsSynthesizer
{
    private readonly GoogleCloudTtsSynthesizer _googleCloudSynthesizer;
    private readonly EdgeNeuralTtsSynthesizer _edgeNeuralSynthesizer;
    private readonly LocalFallbackTtsSynthesizer _localFallbackSynthesizer;
    private readonly ILogger<CompositeTtsSynthesizer> _logger;

    public string ProviderName => "CompositeTts";

    public bool IsConfigured => true;

    public CompositeTtsSynthesizer(
        GoogleCloudTtsSynthesizer googleCloudSynthesizer,
        EdgeNeuralTtsSynthesizer edgeNeuralSynthesizer,
        LocalFallbackTtsSynthesizer localFallbackSynthesizer,
        ILogger<CompositeTtsSynthesizer> logger)
    {
        _googleCloudSynthesizer = googleCloudSynthesizer;
        _edgeNeuralSynthesizer = edgeNeuralSynthesizer;
        _localFallbackSynthesizer = localFallbackSynthesizer;
        _logger = logger;
    }

    public async Task<TtsSynthesisResult> SynthesizeAsync(
        string text,
        string? voiceOrLanguage = null,
        CancellationToken cancellationToken = default)
    {
        // ── Tier 1: Future Google Cloud TTS (if configured) ──────────────────────────
        if (_googleCloudSynthesizer.IsConfigured)
        {
            _logger.LogInformation("🎯 [Tier 1] Google Cloud TTS is configured. Attempting synthesis...");
            var gcpResult = await _googleCloudSynthesizer.SynthesizeAsync(text, voiceOrLanguage, cancellationToken);
            if (gcpResult.Success && gcpResult.AudioData is { Length: > 0 })
            {
                return gcpResult;
            }

            _logger.LogWarning("⚠️ [Tier 1 -> Tier 2] Google Cloud TTS failed ({Error}). Dropping to Edge Neural TTS...",
                gcpResult.ErrorMessage ?? "Unknown");
        }
        else
        {
            _logger.LogDebug("ℹ️ [Tier 1 Skipped] No Google Cloud API key detected. Using default Option B (Edge Neural TTS).");
        }

        // ── Tier 2: Microsoft Edge Neural TTS (Option B, Primary Default) ─────────────
        _logger.LogInformation("⚡ [Tier 2] Synthesizing via Microsoft Edge Neural TTS (en-NG, 100% Free)...");
        var edgeResult = await _edgeNeuralSynthesizer.SynthesizeAsync(text, voiceOrLanguage, cancellationToken);
        if (edgeResult.Success && edgeResult.AudioData is { Length: > 0 })
        {
            return edgeResult;
        }

        _logger.LogWarning("⚠️ [Tier 2 -> Tier 3] Edge Neural TTS failed ({Error}). Engaging Tier 3 Local Offline Fallback...",
            edgeResult.ErrorMessage ?? "Unknown");

        // ── Tier 3: Local Offline Fallback Safety Net ───────────────────────────────────
        _logger.LogInformation("🛡️ [Tier 3] Engaging Local Fallback Synthesizer to guarantee resilient operation...");
        return await _localFallbackSynthesizer.SynthesizeAsync(text, voiceOrLanguage, cancellationToken);
    }
}
