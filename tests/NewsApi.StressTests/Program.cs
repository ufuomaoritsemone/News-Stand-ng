using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;

namespace NewsApi.StressTests;

public static class Program
{
    private const string DefaultApiKey = "AIzaSyAh7soVCbg-JfW-gUKVZIQkbd0sdlE34QI";

    public static async Task<int> Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════════╗
║        News Stand NG - High-Concurrency Stress Test Suite     ║
║                  Powered by NBomber Engine                    ║
╚═══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();

        var baseUrl = ParseArg(args, "--url", "http://localhost:5000");
        var apiKey = ParseArg(args, "--key", DefaultApiKey);
        var targetScenario = ParseArg(args, "--scenario", "all").ToLowerInvariant();
        var durationSec = int.Parse(ParseArg(args, "--duration", "30"));
        var warmupSec = int.Parse(ParseArg(args, "--warmup", "5"));
        var maxRate = int.Parse(ParseArg(args, "--rate", "150"));
        var bypassRateLimit = args.Any(a => string.Equals(a, "--bypass-rate-limit", StringComparison.OrdinalIgnoreCase));

        Console.WriteLine($"Target Base URL : {baseUrl}");
        Console.WriteLine($"Target Scenario : {targetScenario}");
        Console.WriteLine($"Peak Rate (RPS) : {maxRate} req/sec");
        Console.WriteLine($"Warm-up         : {warmupSec} seconds");
        Console.WriteLine($"Test Duration   : {durationSec} seconds");
        Console.WriteLine($"Rate Limit      : {(bypassRateLimit ? "Bypassed (Backend Saturation Mode)" : "Enforced (Client Defense Mode)")}\n");

        using var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            MaxConnectionsPerServer = 1000,
            EnableMultipleHttp2Connections = true
        };

        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        if (bypassRateLimit)
        {
            httpClient.DefaultRequestHeaders.Add("X-Bypass-Rate-Limit", apiKey);
        }

        // Pre-flight check: verify API connectivity
        try
        {
            Console.Write("Checking API connectivity... ");
            var ping = await httpClient.GetAsync("/health");
            if (ping.IsSuccessStatusCode)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"ONLINE (HTTP {(int)ping.StatusCode})");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"WARNING: Received HTTP {(int)ping.StatusCode}");
            }
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Note: Base URL check gave: {ex.Message} (continuing test run...)");
            Console.ResetColor();
        }

        var scenarios = new List<ScenarioProps>();

        // Scenario 1: Main Feed Readers (High-frequency concurrent reads)
        if (targetScenario is "all" or "feed")
        {
            var feedScenario = Scenario.Create("feed_read_spike", async context =>
            {
                try
                {
                    using var response = await httpClient.GetAsync("/api/v1/articles/briefings?topPerCategory=5", context.ScenarioCancellationToken);
                    var bytes = response.Content.Headers.ContentLength ?? 0;
                    return response.IsSuccessStatusCode
                        ? Response.Ok(statusCode: ((int)response.StatusCode).ToString(), sizeBytes: bytes)
                        : Response.Fail(statusCode: ((int)response.StatusCode).ToString(), message: $"HTTP {(int)response.StatusCode}");
                }
                catch (Exception ex)
                {
                    return Response.Fail(statusCode: "Exception", message: ex.Message);
                }
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(warmupSec))
            .WithLoadSimulations(
                Simulation.RampingInject(rate: maxRate, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(durationSec))
            );

            scenarios.Add(feedScenario);
        }

        // Scenario 2: Search & Category Filtering (Database query & index stress)
        if (targetScenario is "all" or "search")
        {
            var categories = new[] { "politics", "business", "sports", "tech", "entertainment", "international" };
            var searchTerms = new[] { "nigeria", "lagos", "president", "economy", "football", "court" };

            var searchScenario = Scenario.Create("search_category_filter", async context =>
            {
                var cat = categories[context.InvocationNumber % categories.Length];
                var term = searchTerms[context.InvocationNumber % searchTerms.Length];
                var url = $"/api/v1/articles?category={cat}&search={term}&page=1&pageSize=10";

                try
                {
                    using var response = await httpClient.GetAsync(url, context.ScenarioCancellationToken);
                    var bytes = response.Content.Headers.ContentLength ?? 0;
                    return response.IsSuccessStatusCode
                        ? Response.Ok(statusCode: ((int)response.StatusCode).ToString(), sizeBytes: bytes)
                        : Response.Fail(statusCode: ((int)response.StatusCode).ToString(), message: $"HTTP {(int)response.StatusCode}");
                }
                catch (Exception ex)
                {
                    return Response.Fail(statusCode: "Exception", message: ex.Message);
                }
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(warmupSec))
            .WithLoadSimulations(
                Simulation.RampingInject(rate: Math.Max(20, maxRate / 2), interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(durationSec))
            );

            scenarios.Add(searchScenario);
        }

        // Scenario 3: Scraper Ingestion Burst (Write contention & duplicate detection stress)
        if (targetScenario is "all" or "ingest")
        {
            var ingestScenario = Scenario.Create("scraper_write_contention", async context =>
            {
                var payload = new
                {
                    articles = new[]
                    {
                        new
                        {
                            title = $"Stress Test Article #{context.InvocationNumber} - {Guid.NewGuid():N}",
                            summary = "Automated high-concurrency ingestion stress test article summary.",
                            content = "Detailed content for stress test payload to measure write latency and DB commit throughput under load.",
                            url = $"https://stresstest.example.com/articles/{context.InvocationNumber}/{Guid.NewGuid():N}",
                            source = "StressTestScraper",
                            category = "general",
                            publishedAt = DateTime.UtcNow
                        }
                    }
                };

                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                try
                {
                    using var response = await httpClient.PostAsync("/api/v1/articles/ingest", content, context.ScenarioCancellationToken);
                    var bytes = response.Content.Headers.ContentLength ?? 0;
                    return response.IsSuccessStatusCode
                        ? Response.Ok(statusCode: ((int)response.StatusCode).ToString(), sizeBytes: bytes)
                        : Response.Fail(statusCode: ((int)response.StatusCode).ToString(), message: $"HTTP {(int)response.StatusCode}");
                }
                catch (Exception ex)
                {
                    return Response.Fail(statusCode: "Exception", message: ex.Message);
                }
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(warmupSec))
            .WithLoadSimulations(
                Simulation.RampingInject(rate: Math.Max(10, maxRate / 5), interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(durationSec))
            );

            scenarios.Add(ingestScenario);
        }

        // Scenario 4: Audio Streaming / Discovery Requests
        if (targetScenario is "all" or "audio")
        {
            var audioScenario = Scenario.Create("audio_discovery_streaming", async context =>
            {
                try
                {
                    using var response = await httpClient.GetAsync("/api/v1/audio/briefings/latest", context.ScenarioCancellationToken);
                    var status = (int)response.StatusCode;
                    var bytes = response.Content.Headers.ContentLength ?? 0;

                    if (status is >= 200 and < 300)
                    {
                        return Response.Ok(statusCode: status.ToString(), sizeBytes: bytes);
                    }

                    return Response.Fail(statusCode: status.ToString(), message: $"HTTP {status}");
                }
                catch (Exception ex)
                {
                    return Response.Fail(statusCode: "Exception", message: ex.Message);
                }
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(warmupSec))
            .WithLoadSimulations(
                Simulation.Inject(rate: Math.Max(10, maxRate / 4), interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(durationSec))
            );

            scenarios.Add(audioScenario);
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Registering {scenarios.Count} scenario(s)...");
        Console.ResetColor();

        NBomberRunner
            .RegisterScenarios(scenarios.ToArray())
            .WithReportFolder("reports/stress_tests")
            .WithReportFormats(ReportFormat.Html, ReportFormat.Txt, ReportFormat.Md)
            .Run();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n[✓] Stress test completed! Reports saved in 'reports/stress_tests'");
        Console.ResetColor();

        return 0;
    }

    private static string ParseArg(string[] args, string flag, string defaultValue)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return defaultValue;
    }
}
