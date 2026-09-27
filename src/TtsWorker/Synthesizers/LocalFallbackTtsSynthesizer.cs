using System.Text;
using Microsoft.Extensions.Logging;

namespace TtsWorker.Synthesizers;

/// <summary>
/// Architecture Component: Tri-Tier TTS Architecture — Tier 3: Local Offline Safety Net.
/// 
/// Purpose:
/// Guarantees that TtsWorker and the Nigerian News Grid pipeline never crash or experience fatal
/// job abortions during complete internet outages, DNS failures, or upstream cloud rate-limits.
/// 
/// Implementation:
/// Generates a valid, playable RIFF WAVE audio stream (16-bit PCM Mono, 16,000 Hz) containing an
/// acoustic news bulletin chime (D5: 587.33 Hz transitioning to A5: 880 Hz with exponential decay).
/// This provides a fully playable, standardized audio container that any client (browser, mobile app,
/// test harness) can stream and decode without requiring third-party codecs or external network access.
/// </summary>
public sealed class LocalFallbackTtsSynthesizer : ITtsSynthesizer
{
    private readonly ILogger<LocalFallbackTtsSynthesizer> _logger;

    public string ProviderName => "LocalFallback";

    // Always configured because it has zero external dependencies or API keys
    public bool IsConfigured => true;

    public LocalFallbackTtsSynthesizer(ILogger<LocalFallbackTtsSynthesizer> logger)
    {
        _logger = logger;
    }

    public Task<TtsSynthesisResult> SynthesizeAsync(
        string text,
        string? voiceOrLanguage = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating local fallback audio chime container for text payload ({Length} characters)...", text?.Length ?? 0);

            var audioBytes = GenerateChimeWaveAudio();

            return Task.FromResult(new TtsSynthesisResult(
                Success: true,
                AudioData: audioBytes,
                ContentType: "audio/wav",
                ProviderUsed: ProviderName
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating local fallback audio");
            return Task.FromResult(new TtsSynthesisResult(
                Success: false,
                AudioData: null,
                ContentType: "audio/wav",
                ProviderUsed: ProviderName,
                ErrorMessage: ex.Message
            ));
        }
    }

    /// <summary>
    /// Constructs a valid RIFF WAVE (PCM 16-bit, 16000Hz, Mono) audio buffer
    /// with a warm acoustic broadcast chime (D5 587.33 Hz & A5 880.00 Hz).
    /// </summary>
    public static byte[] GenerateChimeWaveAudio(int durationSeconds = 2)
    {
        const int sampleRate = 16000;
        const short bitsPerSample = 16;
        const short channels = 1;
        int totalSamples = sampleRate * Math.Clamp(durationSeconds, 1, 5);

        using var memoryStream = new MemoryStream();
        using var writer = new BinaryWriter(memoryStream);

        // 1. RIFF Header
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        int dataChunkSize = totalSamples * (bitsPerSample / 8) * channels;
        writer.Write(36 + dataChunkSize); // File size - 8
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        // 2. "fmt " Subchunk
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);                                 // Subchunk1Size for PCM
        writer.Write((short)1);                           // AudioFormat: 1 = PCM
        writer.Write(channels);                           // NumChannels: 1 (Mono)
        writer.Write(sampleRate);                         // SampleRate: 16000
        writer.Write(sampleRate * channels * (bitsPerSample / 8)); // ByteRate
        writer.Write((short)(channels * (bitsPerSample / 8)));       // BlockAlign
        writer.Write(bitsPerSample);                      // BitsPerSample: 16

        // 3. "data" Subchunk
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataChunkSize);

        // 4. PCM Samples: Generate a 2-tone melodic news radio chime
        // Tone 1 (0 to 45% of duration): 587.33 Hz (D5)
        // Tone 2 (45% to 100% of duration): 880.00 Hz (A5)
        double tone1Freq = 587.33;
        double tone2Freq = 880.00;
        int splitSample = (int)(totalSamples * 0.45);

        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / sampleRate;
            double freq;
            double envelope;

            if (i < splitSample)
            {
                freq = tone1Freq;
                // Decay envelope for first tone
                envelope = Math.Exp(-3.0 * (double)i / splitSample);
            }
            else
            {
                freq = tone2Freq;
                // Decay envelope for second tone
                int sampleInTone2 = i - splitSample;
                int remainingSamples = totalSamples - splitSample;
                envelope = Math.Exp(-3.5 * (double)sampleInTone2 / remainingSamples);
            }

            // Sine wave calculation with gentle harmonic overtone (2nd harmonic at 15% amplitude)
            double sampleValue = Math.Sin(2.0 * Math.PI * freq * t) * 0.85 
                               + Math.Sin(4.0 * Math.PI * freq * t) * 0.15;

            // Apply amplitude envelope and master volume (0.5 to prevent digital clipping)
            short pcmSample = (short)(sampleValue * envelope * 0.5 * short.MaxValue);
            writer.Write(pcmSample);
        }

        writer.Flush();
        return memoryStream.ToArray();
    }
}
