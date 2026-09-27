using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TtsWorker.Synthesizers;

// ── Nigerian News Grid - TtsWorker Entry Point ──────────────────────────────────
// Background worker service responsible for generating high-definition audio news briefings
// for morning (8:00 AM WAT) and evening (6:00 PM WAT) broadcast windows.
// Features a Tri-Tier Speech Engine:
//   Tier 1: Google Cloud Text-to-Speech (active if GCP_TTS_API_KEY is supplied)
//   Tier 2: Microsoft Edge Neural TTS (Option B, default 100% free en-NG Nigerian voice)
//   Tier 3: Local Offline Fallback (generates valid audio container during network outages)

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddHttpClient();

        // Register Tri-Tier Synthesizers
        services.AddSingleton<GoogleCloudTtsSynthesizer>();
        services.AddSingleton<EdgeNeuralTtsSynthesizer>();
        services.AddSingleton<LocalFallbackTtsSynthesizer>();
        services.AddSingleton<ITtsSynthesizer, CompositeTtsSynthesizer>();

        services.AddHostedService<TtsWorker.Service>();
    })
    .Build();

await host.RunAsync();
