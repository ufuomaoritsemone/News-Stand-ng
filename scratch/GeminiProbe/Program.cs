using System;
using System.Linq;
using System.Threading.Tasks;
using Google.GenAI;
using Google.GenAI.Types;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("==================================================");
        Console.WriteLine(" GEMINI & VERTEX AI CONNECTIVITY PROBE");
        Console.WriteLine("==================================================");

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "NigerianNewGrid.slnx")) && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }
        string root = dir?.FullName ?? Directory.GetCurrentDirectory();
        string envPath = Path.Combine(root, ".env");
        string saPath = Path.Combine(root, "secrets", "gcp-service-account.json");

        string? apiKey = null;
        if (System.IO.File.Exists(envPath))
        {
            foreach (var line in System.IO.File.ReadAllLines(envPath))
            {
                if (line.Trim().StartsWith("GEMINI_API_KEY="))
                {
                    apiKey = line.Split('=', 2)[1].Trim();
                }
            }
        }

        Console.WriteLine($"Repository Root: {root}");
        Console.WriteLine($"Service Account: {(System.IO.File.Exists(saPath) ? "EXISTS" : "MISSING")} ({saPath})");
        Console.WriteLine($"API Key:         {(string.IsNullOrWhiteSpace(apiKey) ? "MISSING" : apiKey.Substring(0, Math.Min(12, apiKey.Length)) + "...")}");
        Console.WriteLine();

        // TEST 1: Google AI Studio Client with API Key
        Console.WriteLine("--- TEST 1: Google AI Studio Client (API Key) ---");
        System.Environment.SetEnvironmentVariable("GOOGLE_GENAI_USE_VERTEXAI", null);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var client = new Client(apiKey: apiKey);
                Console.WriteLine("Probing gemini-embedding-001 via Google AI Studio...");
                var embedResult = await client.Models.EmbedContentAsync(
                    model: "gemini-embedding-001",
                    contents: "News Stand NG connectivity probe."
                );

                if (embedResult?.Embeddings?.FirstOrDefault()?.Values is { Count: > 0 } vals)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[SUCCESS] Generated {vals.Count}-dim vector embedding!");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine("[RESPONSE] Received empty embeddings.");
                }

                Console.WriteLine("Probing gemini-3.1-flash-lite via Google AI Studio...");
                var genResult = await client.Models.GenerateContentAsync(
                    model: "gemini-3.1-flash-lite",
                    contents: "Respond in 3 words: Ready"
                );
                var genText = genResult?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                if (!string.IsNullOrWhiteSpace(genText))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[SUCCESS] gemini-3.1-flash-lite responded: \"{genText.Trim()}\"");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[FAILED] {ex.GetType().Name}: {ex.Message}");
                Console.ResetColor();
            }
        }
        else
        {
            Console.WriteLine("Skipped: No API Key.");
        }

        Console.WriteLine();

        string gcloudAdcPath = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
            "gcloud",
            "application_default_credentials.json"
        );

        Console.WriteLine($"gcloud User ADC: {(System.IO.File.Exists(gcloudAdcPath) ? "EXISTS" : "MISSING")} ({gcloudAdcPath})");

        // TEST 2: Vertex AI via gcloud User ADC
        Console.WriteLine("--- TEST 2: Google Cloud Vertex AI (gcloud User ADC) ---");
        if (System.IO.File.Exists(gcloudAdcPath))
        {
            System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", gcloudAdcPath);
            System.Environment.SetEnvironmentVariable("GOOGLE_GENAI_USE_VERTEXAI", "true");
            System.Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", "project-00ae160d-e8ea-44ec-9ff");
            System.Environment.SetEnvironmentVariable("GCP_LOCATION", "us-central1");

            try
            {
                Console.WriteLine($"Initializing Vertex AI Client with ADC from: {gcloudAdcPath}...");
                var vertexClient = new Client();
                Console.WriteLine("Probing text-embedding-004 via Vertex AI...");

                var embedResult = await vertexClient.Models.EmbedContentAsync(
                    model: "text-embedding-004",
                    contents: "News Stand NG Vertex AI user ADC connectivity probe."
                );

                if (embedResult?.Embeddings?.FirstOrDefault()?.Values is { Count: > 0 } vals)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[SUCCESS] Vertex AI (User ADC) generated {vals.Count}-dim vector embedding!");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine("[RESPONSE] Vertex AI response empty.");
                }

                Console.WriteLine();
                Console.WriteLine("Probing Gemini Generative Text via Vertex AI (gemini-2.5-flash / gemini-2.0-flash)...");
                var vertexModels = new[] { "gemini-2.5-flash", "gemini-2.0-flash", "gemini-1.5-flash" };
                foreach (var vm in vertexModels)
                {
                    try
                    {
                        var genResult = await vertexClient.Models.GenerateContentAsync(
                            model: vm,
                            contents: "Respond in 5 words or fewer: Is Gemini connected?"
                        );

                        var text = genResult?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[SUCCESS] Vertex AI {vm} responded: \"{text.Trim()}\"");
                            Console.ResetColor();
                            break;
                        }
                    }
                    catch (Exception gEx)
                    {
                        Console.WriteLine($"  {vm}: {gEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[FAILED] {ex.GetType().Name}: {ex.Message}");
                Console.ResetColor();
            }
        }
        else
        {
            Console.WriteLine("Skipped: No gcloud User ADC found.");
        }

        Console.WriteLine();

        // TEST 3: Vertex AI via Service Account ADC (if present)
        Console.WriteLine("--- TEST 3: Google Cloud Vertex AI (Service Account Key) ---");
        if (System.IO.File.Exists(saPath))
        {
            System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", saPath);
            System.Environment.SetEnvironmentVariable("GOOGLE_GENAI_USE_VERTEXAI", "true");
            System.Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", "project-00ae160d-e8ea-44ec-9ff");
            System.Environment.SetEnvironmentVariable("GCP_LOCATION", "us-central1");

            try
            {
                Console.WriteLine("Initializing Vertex AI Client from Service Account Key...");
                var vertexClient = new Client();
                var embedResult = await vertexClient.Models.EmbedContentAsync(
                    model: "text-embedding-004",
                    contents: "News Stand NG Vertex AI service account probe."
                );

                if (embedResult?.Embeddings?.FirstOrDefault()?.Values is { Count: > 0 } vals)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[SUCCESS] Vertex AI (SA) generated {vals.Count}-dim vector embedding!");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[FAILED] {ex.GetType().Name}: {ex.Message}");
                Console.ResetColor();
            }
        }
        else
        {
            Console.WriteLine("Skipped: No Service Account JSON.");
        }

        Console.WriteLine();
        Console.WriteLine("==================================================");
        Console.WriteLine(" PROBE COMPLETE");
        Console.WriteLine("==================================================");
    }
}
