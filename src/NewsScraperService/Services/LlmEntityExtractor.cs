using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsScraperService.Services;

public class LlmEntityExtractor : IEntityExtractor
{
    private readonly GazetteerEntityExtractor _gazetteer;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LlmEntityExtractor> _logger;

    public LlmEntityExtractor(
        GazetteerEntityExtractor gazetteer,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<LlmEntityExtractor> logger)
    {
        _gazetteer = gazetteer;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<EntityMatch>> ExtractEntitiesAsync(
        string title,
        string? summary = null,
        string? content = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Run Tier 1 High-Precision Gazetteer
        var matches = await _gazetteer.ExtractEntitiesAsync(title, summary, content, cancellationToken);

        // If Tier 1 found >= 2 entities or LLM extraction is disabled, return Tier 1 results
        var enabled = _config.GetValue<bool>("LlmEntityExtraction:Enabled", false);
        if (matches.Count >= 2 || !enabled)
        {
            return matches;
        }

        // 2. Tier 2 LLM Extraction
        var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? _config["Gemini:ApiKey"]
            ?? _config["Gemini__ApiKey"]
            ?? _config["GoogleCloud:ApiKey"]
            ?? _config["LlmEntityExtraction:ApiKey"]
            ?? _config["ApiKey"]
            ?? Environment.GetEnvironmentVariable("API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogDebug("Tier 2 LLM entity extraction skipped: No API key configured.");
            return matches;
        }

        try
        {
            var llmEntities = await CallLlmExtractorAsync(title, summary, apiKey, cancellationToken);
            var seenSlugs = new HashSet<string>(matches.Select(m => m.Slug), StringComparer.OrdinalIgnoreCase);

            foreach (var entity in llmEntities)
            {
                if (!seenSlugs.Contains(entity.Slug))
                {
                    matches.Add(entity);
                    seenSlugs.Add(entity.Slug);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tier 2 LLM entity extraction failed. Falling back gracefully to Tier 1 matches.");
        }

        return matches;
    }

    private async Task<List<EntityMatch>> CallLlmExtractorAsync(
        string title,
        string? summary,
        string apiKey,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(25);

        var prompt = $$"""
            Extract up to 3 named Nigerian entities (Persons, Organizations, Places, or Themes) from this news story.
            Output JSON only as an array of objects with keys: "Name", "Slug", "Type" (Person, Organization, Place, Theme), "Description".
            Story Title: {{title}}
            Story Summary: {{summary}}
            """;

        var model = Environment.GetEnvironmentVariable("GEMINI_MODEL")
            ?? _config["Gemini:Model"]
            ?? _config["Gemini__Model"]
            ?? "gemini-3.1-flash-lite";
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        _logger.LogInformation(
            "\n╔══════════════════════════════════════════════════════════════════════════════╗\n" +
            "║ 🏷️ [GEMINI NER ENGAGED] Tier 2 Named Entity Extraction                        ║\n" +
            "║ Story: '{Title}' | Model: {Model}                                            ║\n" +
            "╚══════════════════════════════════════════════════════════════════════════════╝",
            title, model);

        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                thinkingConfig = new
                {
                    thinkingBudget = 0
                }
            }
        };

        var jsonContent = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
        var sw = Stopwatch.StartNew();
        var response = await client.PostAsync(endpoint, jsonContent, ct);
        sw.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("❌ [GEMINI NER FAILED] Returned HTTP {Status} for '{Title}' ({ElapsedMs} ms)",
                response.StatusCode, title, sw.ElapsedMilliseconds);
            return [];
        }

        var responseString = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseString);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text)) return [];

        var parsed = JsonSerializer.Deserialize<List<EntityDefinition>>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (parsed == null) return [];

        var results = parsed.Select(p => new EntityMatch
        {
            Name = p.Name,
            Slug = string.IsNullOrWhiteSpace(p.Slug) ? GenerateSlug(p.Name) : p.Slug.ToLowerInvariant(),
            Type = p.Type,
            Description = p.Description,
            Confidence = 0.80f,
            SourceTier = "LLM"
        }).ToList();

        _logger.LogInformation("✨ [GEMINI NER SUCCESS] Extracted {Count} entities for '{Title}' ({ElapsedMs} ms)",
            results.Count, title, sw.ElapsedMilliseconds);

        return results;
    }

    private static string GenerateSlug(string text)
    {
        var clean = System.Text.RegularExpressions.Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9\s-]", "");
        return System.Text.RegularExpressions.Regex.Replace(clean, @"\s+", "-").Trim('-');
    }
}
