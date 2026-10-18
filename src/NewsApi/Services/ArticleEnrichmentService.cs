using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services.Embeddings;

namespace NewsApi.Services;

public partial class ArticleEnrichmentService(
    NewsDbContext db,
    IEmbeddingProvider embeddingProvider,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<ArticleEnrichmentService> logger) : IArticleEnrichmentService
{
    private readonly NewsDbContext _db = db;
    private readonly IEmbeddingProvider _embeddingProvider = embeddingProvider;
    private readonly IConfiguration _config = config;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<ArticleEnrichmentService> _logger = logger;

    // In-memory circuit breaker state: if Cloud AI fails, all subsequent articles in the batch
    // and subsequent requests skip Cloud AI and immediately use local ML (TextRank) until cooldown expires.
    private static DateTime _circuitBreakerUntilUtc = DateTime.MinValue;
    private static readonly TimeSpan CircuitBreakerCooldown = TimeSpan.FromMinutes(10);
    private static readonly object CircuitLock = new();

    public bool IsCircuitBreakerActive => IsCircuitBreakerOpen();

    public static bool IsCircuitBreakerOpen()
    {
        lock (CircuitLock)
        {
            return DateTime.UtcNow < _circuitBreakerUntilUtc;
        }
    }

    public static void TripCircuitBreaker(string reason, ILogger logger)
    {
        lock (CircuitLock)
        {
            _circuitBreakerUntilUtc = DateTime.UtcNow.Add(CircuitBreakerCooldown);
        }
        logger.LogWarning(
            "Cloud AI Circuit Breaker TRIPPED until {UntilUtc:u}: {Reason}. Falling back to local ML (TextRank) for batch and subsequent requests.",
            _circuitBreakerUntilUtc, reason);
    }

    public static void ResetCircuitBreaker()
    {
        lock (CircuitLock)
        {
            _circuitBreakerUntilUtc = DateTime.MinValue;
        }
    }

    public Task<bool> IsCloudAiAvailableAsync(CancellationToken cancellationToken = default)
    {
        var geminiKey = _config["Gemini:ApiKey"];
        var geminiEnabled = _config.GetValue<bool>("AiEnrichment:GeminiEnabled", false);

        if (string.IsNullOrWhiteSpace(geminiKey) || !geminiEnabled)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(!IsCircuitBreakerOpen());
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(?<!\b(?:Mr|Mrs|Ms|Dr|Prof|Sen|Gov|Gen|Col|Capt|Hon|Justice|Alh|Chief|Pres|Brig|Rev|Pst|Ltd|Inc|Corp|Co|vs|etc|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Oct|Nov|Dec|No|Vol)\.)(?<=[.!?])\s+(?=[A-Z0-9""“'‘₦])", RegexOptions.Compiled)]
    private static partial Regex SentenceSplitRegex();

    [GeneratedRegex(@"\b(?:\d{1,2}(?:st|nd|rd|th)?\s+(?:January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*|\b(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)\b|\b(?:yesterday|today|last week|earlier this week)\b|\b202[0-9]\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DateTemporalRegex();

    [GeneratedRegex(@"(?:₦|\$|Naira|dollars?)\s*[\d,.]+(?:\s*(?:trillion|billion|million|k))?|\b\d+(?:\.\d+)?%", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex FinancialNumericRegex();

    public async Task<ArticleEnrichment?> EnrichArticleAsync(string articleId, CancellationToken cancellationToken = default)
    {
        var article = await _db.Articles
            .Include(a => a.ArticleTopics).ThenInclude(at => at.Topic)
            .Include(a => a.Enrichment)
            .FirstOrDefaultAsync(a => a.Id == articleId, cancellationToken);

        if (article == null)
        {
            _logger.LogWarning("Article {ArticleId} not found for enrichment", articleId);
            return null;
        }

        var knownEntities = article.ArticleTopics
            .Where(at => at.Topic != null)
            .Select(at => at.Topic!.Name)
            .ToList();

        var result = await GenerateEnrichmentAsync(
            article.Title,
            article.Summary,
            article.Content,
            article.Category,
            knownEntities,
            cancellationToken);

        if (article.Enrichment != null)
        {
            article.Enrichment.KeyTakeaways = result.KeyTakeaways;
            article.Enrichment.TimelineFacts = result.TimelineFacts;
            article.Enrichment.Faqs = result.Faqs;
            article.Enrichment.FullSummary = result.FullSummary;
            article.Enrichment.Engine = result.Engine;
            article.Enrichment.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var enrichment = new ArticleEnrichment
            {
                ArticleId = article.Id,
                KeyTakeaways = result.KeyTakeaways,
                TimelineFacts = result.TimelineFacts,
                Faqs = result.Faqs,
                FullSummary = result.FullSummary,
                Engine = result.Engine,
                CreatedAt = DateTime.UtcNow
            };
            _db.ArticleEnrichments.Add(enrichment);
            article.Enrichment = enrichment;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Successfully enriched article {ArticleId} ({Title}) using engine {Engine}",
            article.Id, article.Title, result.Engine);

        return article.Enrichment;
    }

    public async Task<int> ProcessUnenrichedBatchAsync(int batchSize = 25, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-14);

        var existingEnrichedIds = await _db.ArticleEnrichments
            .Select(e => e.ArticleId)
            .ToListAsync(cancellationToken);

        var unenrichedArticles = await _db.Articles
            .Include(a => a.ArticleTopics).ThenInclude(at => at.Topic)
            .Where(a => a.PublishedAt >= cutoff && !existingEnrichedIds.Contains(a.Id))
            .OrderByDescending(a => a.PublishedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (unenrichedArticles.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("Processing enrichment for batch of {Count} articles", unenrichedArticles.Count);

        var geminiKey = _config["Gemini:ApiKey"];
        var geminiEnabled = _config.GetValue<bool>("AiEnrichment:GeminiEnabled", false);

        // Pre-flight check: determine whether Cloud AI is eligible for this batch
        bool useCloudAi = !string.IsNullOrWhiteSpace(geminiKey) && geminiEnabled && !IsCircuitBreakerOpen();

        if (geminiEnabled && !string.IsNullOrWhiteSpace(geminiKey) && IsCircuitBreakerOpen())
        {
            _logger.LogInformation(
                "Batch enrichment: Cloud AI circuit breaker is ACTIVE (cooldown until {Cooldown:u}). Processing entire batch with local ML (TextRank).",
                _circuitBreakerUntilUtc);
        }
        else if (useCloudAi)
        {
            _logger.LogInformation("Batch enrichment: Cloud AI is ENABLED. Probing availability with batch.");
        }
        else
        {
            _logger.LogInformation("Batch enrichment: Cloud AI is disabled or unconfigured. Processing batch with local ML (TextRank).");
        }

        int count = 0;
        foreach (var article in unenrichedArticles)
        {
            try
            {
                var knownEntities = article.ArticleTopics
                    .Where(at => at.Topic != null)
                    .Select(at => at.Topic!.Name)
                    .ToList();

                // If useCloudAi is false (either unconfigured, circuit breaker already active, or tripped by a previous article in this batch),
                // pass skipCloudAi: true to directly use TextRank with 0 network calls.
                var result = await GenerateEnrichmentAsync(
                    article.Title,
                    article.Summary,
                    article.Content,
                    article.Category,
                    knownEntities,
                    skipCloudAi: !useCloudAi,
                    cancellationToken);

                // If Cloud AI was attempted on this article but failed and returned TextRank-Extractive,
                // that means Cloud AI is unavailable! Flip useCloudAi = false for the entire remainder of the batch!
                if (useCloudAi && result.Engine == "TextRank-Extractive")
                {
                    _logger.LogWarning(
                        "Batch enrichment: Cloud AI failed for article '{ArticleId}'. Tripping circuit breaker and falling back to local ML for all remaining {Remaining} articles in batch.",
                        article.Id, unenrichedArticles.Count - count - 1);
                    useCloudAi = false;
                }

                var enrichment = new ArticleEnrichment
                {
                    ArticleId = article.Id,
                    KeyTakeaways = result.KeyTakeaways,
                    TimelineFacts = result.TimelineFacts,
                    Faqs = result.Faqs,
                    FullSummary = result.FullSummary,
                    Engine = result.Engine,
                    CreatedAt = DateTime.UtcNow
                };

                _db.ArticleEnrichments.Add(enrichment);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich article {Id}", article.Id);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Enrichment batch complete: saved {Count} enrichments", count);
        return count;
    }

    public Task<ArticleEnrichmentResult> GenerateEnrichmentAsync(
        Article article,
        CancellationToken cancellationToken = default)
    {
        var entities = article.ArticleTopics?
            .Select(at => at.Topic?.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .Cast<string>();
        return GenerateEnrichmentAsync(
            article.Title,
            article.Summary,
            article.Content,
            article.Category,
            entities,
            skipCloudAi: false,
            cancellationToken);
    }

    public Task<ArticleEnrichmentResult> GenerateEnrichmentAsync(
        string title,
        string? summary,
        string? content,
        string? category,
        IEnumerable<string>? knownEntities = null,
        CancellationToken cancellationToken = default)
        => GenerateEnrichmentAsync(title, summary, content, category, knownEntities, skipCloudAi: false, cancellationToken);

    public async Task<ArticleEnrichmentResult> GenerateEnrichmentAsync(
        string title,
        string? summary,
        string? content,
        string? category,
        IEnumerable<string>? knownEntities,
        bool skipCloudAi,
        CancellationToken cancellationToken = default)
    {
        // 1. Check optional Gemini enhancement if not explicitly skipped and circuit breaker is closed
        var geminiKey = _config["Gemini:ApiKey"];
        var geminiEnabled = _config.GetValue<bool>("AiEnrichment:GeminiEnabled", false);

        if (!skipCloudAi && !IsCircuitBreakerOpen() && !string.IsNullOrWhiteSpace(geminiKey) && geminiEnabled)
        {
            var geminiResult = await TryEnrichWithGeminiAsync(title, summary, content, category, geminiKey, cancellationToken);
            if (geminiResult != null)
            {
                return geminiResult;
            }

            // Cloud AI failed or timed out: trip the circuit breaker immediately
            TripCircuitBreaker("Gemini enrichment failed, timed out, or returned an error status", _logger);
        }

        // 2. Default Zero-API-Key Local Extractive Engine (TextRank / LexRank)
        return await EnrichWithLocalTextRankAsync(title, summary, content, category, knownEntities, cancellationToken);
    }

    private async Task<ArticleEnrichmentResult> EnrichWithLocalTextRankAsync(
        string title,
        string? summary,
        string? content,
        string? category,
        IEnumerable<string>? knownEntities,
        CancellationToken cancellationToken)
    {
        var entities = knownEntities?.ToList() ?? [];

        // 1. Gather text sources and split into sentences
        var rawText = string.Empty;
        if (!string.IsNullOrWhiteSpace(content))
        {
            rawText = content;
        }
        else if (!string.IsNullOrWhiteSpace(summary))
        {
            rawText = title + ". " + summary;
        }
        else
        {
            rawText = title;
        }

        var candidateSentences = SplitSentences(rawText);

        if (candidateSentences.Count == 0)
        {
            // Fallback for extremely short texts
            var fallbackTakeaway = !string.IsNullOrWhiteSpace(summary) ? summary : title;
            var fallbackSummary = new FullSummaryPayload
            {
                ExecutiveOverview = CleanSentence(fallbackTakeaway),
                ContextAndBackground = [$"Published under {category ?? "General"} news."],
                KeyFiguresAndEntities = [],
                OutlookOrNextSteps = "Check publisher source for ongoing coverage.",
                ReadingTimeSeconds = 20
            };
            return new ArticleEnrichmentResult(
                [CleanSentence(fallbackTakeaway)],
                [],
                [new FaqItemDto($"What is this story about?", CleanSentence(fallbackTakeaway))],
                "TextRank-Extractive",
                fallbackSummary
            );
        }

        // 2. Compute sentence embeddings & document centroid
        var sentenceVectors = new List<float[]>();
        foreach (var s in candidateSentences)
        {
            var vec = await _embeddingProvider.GenerateEmbeddingAsync(s, cancellationToken);
            sentenceVectors.Add(vec);
        }

        int dim = sentenceVectors[0].Length;
        var centroid = new float[dim];
        foreach (var v in sentenceVectors)
        {
            for (int d = 0; d < dim; d++) centroid[d] += v[d];
        }
        for (int d = 0; d < dim; d++) centroid[d] /= sentenceVectors.Count;
        VectorMath.Normalize(centroid);

        // 3. Score each sentence: Centroid Salience + Graph Centrality + Inverted Pyramid Position Bias
        var scoredSentences = new List<(string Sentence, float[] Vector, float Score)>();

        for (int i = 0; i < candidateSentences.Count; i++)
        {
            var s = candidateSentences[i];
            var v = sentenceVectors[i];

            // Centroid similarity (thematic relevance)
            float centroidSim = VectorMath.CosineSimilarity(v, centroid);

            // Graph degree centrality against other sentences
            float degreeSim = 0f;
            for (int j = 0; j < candidateSentences.Count; j++)
            {
                if (i != j) degreeSim += VectorMath.CosineSimilarity(v, sentenceVectors[j]);
            }
            if (candidateSentences.Count > 1) degreeSim /= (candidateSentences.Count - 1);

            // Inverted pyramid position multiplier: earlier sentences carry foundational reporting
            float posMultiplier = 1.0f + (0.6f / (1.0f + i * 0.25f));

            // Named entity bonus
            float entityMultiplier = entities.Any(e => s.Contains(e, StringComparison.OrdinalIgnoreCase)) ? 1.25f : 1.0f;

            // Financial / numeric facts bonus
            float factMultiplier = FinancialNumericRegex().IsMatch(s) ? 1.15f : 1.0f;

            // Length balance: penalize excessively short (< 40 chars) or run-on sentences (> 250 chars)
            float lengthMultiplier = (s.Length >= 40 && s.Length <= 250) ? 1.0f : 0.8f;

            float finalScore = (0.65f * centroidSim + 0.35f * degreeSim) * posMultiplier * entityMultiplier * factMultiplier * lengthMultiplier;
            scoredSentences.Add((s, v, finalScore));
        }

        // 4. Maximal Marginal Relevance (MMR) for diverse, non-redundant takeaways
        var orderedByScore = scoredSentences.OrderByDescending(x => x.Score).ToList();
        var selectedTakeaways = new List<(string Sentence, float[] Vector)>();

        foreach (var candidate in orderedByScore)
        {
            if (selectedTakeaways.Count >= 3) break;

            if (selectedTakeaways.Count == 0)
            {
                selectedTakeaways.Add((candidate.Sentence, candidate.Vector));
            }
            else
            {
                // Check redundancy: cosine similarity to any already selected takeaway must be < 0.65
                bool isRedundant = selectedTakeaways.Any(sel => VectorMath.CosineSimilarity(candidate.Vector, sel.Vector) >= 0.65f);
                if (!isRedundant)
                {
                    selectedTakeaways.Add((candidate.Sentence, candidate.Vector));
                }
            }
        }

        // If MMR was overly strict, fill remaining slots from top scored
        int fillIdx = 0;
        while (selectedTakeaways.Count < Math.Min(3, orderedByScore.Count) && fillIdx < orderedByScore.Count)
        {
            var next = orderedByScore[fillIdx++];
            if (!selectedTakeaways.Any(t => t.Sentence == next.Sentence))
            {
                selectedTakeaways.Add((next.Sentence, next.Vector));
            }
        }

        var keyTakeaways = selectedTakeaways
            .Select(t => CleanSentence(t.Sentence))
            .ToList();

        // 5. Timeline Fact Extraction
        var timelineFacts = new List<TimelineFactDto>();
        foreach (var (sentence, _, _) in scoredSentences)
        {
            if (timelineFacts.Count >= 4) break;

            var dateMatch = DateTemporalRegex().Match(sentence);
            var finMatch = FinancialNumericRegex().Match(sentence);

            if (dateMatch.Success || finMatch.Success)
            {
                var matchedDateText = dateMatch.Success ? dateMatch.Value : finMatch.Value;
                var matchedEntity = entities.FirstOrDefault(e => sentence.Contains(e, StringComparison.OrdinalIgnoreCase));

                var cleanedFact = CleanSentence(sentence);
                if (!timelineFacts.Any(f => f.Fact == cleanedFact))
                {
                    timelineFacts.Add(new TimelineFactDto
                    {
                        Fact = cleanedFact,
                        DateText = matchedDateText,
                        Entity = matchedEntity
                    });
                }
            }
        }

        // 6. Structured FAQ Generation
        var faqs = GenerateStructuredFaqs(title, category, entities, keyTakeaways, timelineFacts);

        // 7. Local Full-Body Synthesis Fallback
        var executiveOverview = keyTakeaways.Count > 0
            ? string.Join(" ", keyTakeaways.Take(Math.Min(2, keyTakeaways.Count)))
            : CleanSentence(!string.IsNullOrWhiteSpace(summary) ? summary : title);

        var contextAndBackground = keyTakeaways.Skip(2).Take(2).ToList();
        if (contextAndBackground.Count == 0 && !string.IsNullOrWhiteSpace(category))
        {
            contextAndBackground.Add($"Reported under Nigeria's {category} news sector.");
        }

        var keyFigures = timelineFacts
            .Select(f => !string.IsNullOrWhiteSpace(f.DateText) ? $"{f.DateText}: {f.Fact}" : f.Fact)
            .Take(3)
            .ToList();

        var localFullSummary = new FullSummaryPayload
        {
            ExecutiveOverview = executiveOverview,
            ContextAndBackground = contextAndBackground,
            KeyFiguresAndEntities = keyFigures,
            OutlookOrNextSteps = "Follow original publisher reporting for subsequent developments, official responses, and verified updates.",
            ReadingTimeSeconds = Math.Max(25, candidateSentences.Count * 2)
        };

        return new ArticleEnrichmentResult(keyTakeaways, timelineFacts, faqs, "TextRank-Extractive", localFullSummary);
    }

    private static List<FaqItemDto> GenerateStructuredFaqs(
        string title,
        string? category,
        List<string> entities,
        List<string> takeaways,
        List<TimelineFactDto> facts)
    {
        var faqs = new List<FaqItemDto>();

        // FAQ 1: Core development
        var primaryEntity = entities.FirstOrDefault();
        string q1 = !string.IsNullOrWhiteSpace(primaryEntity)
            ? $"What is the latest development regarding {primaryEntity}?"
            : $"What is the key takeaway from this report?";

        string a1 = takeaways.Count > 0
            ? takeaways[0]
            : title;

        faqs.Add(new FaqItemDto { Question = q1, Answer = a1 });

        // FAQ 2: Key Stakeholders & Involved Entities
        if (entities.Count > 0)
        {
            string entityList = string.Join(", ", entities.Take(3));
            string q2 = $"Who are the key organizations or public figures mentioned?";
            string a2 = takeaways.Count > 1 && entities.Any(e => takeaways[1].Contains(e, StringComparison.OrdinalIgnoreCase))
                ? takeaways[1]
                : $"The primary parties identified in this coverage include {entityList}.";

            faqs.Add(new FaqItemDto { Question = q2, Answer = a2 });
        }
        else if (takeaways.Count > 1)
        {
            faqs.Add(new FaqItemDto
            {
                Question = $"What additional context is highlighted in this report?",
                Answer = takeaways[1]
            });
        }

        // FAQ 3: Timeline or Financial/Policy Impact
        if (facts.Count > 0)
        {
            var firstFact = facts[0];
            string q3 = !string.IsNullOrWhiteSpace(firstFact.DateText)
                ? $"What timeline or key figures were confirmed?"
                : $"What significant data points are reported?";

            faqs.Add(new FaqItemDto
            {
                Question = q3,
                Answer = firstFact.Fact
            });
        }
        else if (takeaways.Count > 2)
        {
            faqs.Add(new FaqItemDto
            {
                Question = $"What are the broader implications of this development?",
                Answer = takeaways[2]
            });
        }

        return faqs;
    }

    private static List<string> SplitSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var normalized = WhitespaceRegex().Replace(text, " ").Trim();
        var rawSentences = SentenceSplitRegex().Split(normalized);

        var list = new List<string>();
        foreach (var s in rawSentences)
        {
            var trimmed = s.Trim();
            if (trimmed.Length >= 25 &&
                !trimmed.StartsWith("READ ALSO", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("Click here", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("Follow us on", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("Copyright", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.Contains("All rights reserved", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(trimmed);
            }
        }

        return list;
    }

    private static string CleanSentence(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var cleaned = text.Trim();
        // Remove leading dash, bullet, or list markers
        cleaned = Regex.Replace(cleaned, @"^[-*•\d\.\)]+\s*", string.Empty);

        if (cleaned.Length > 0 && char.IsLower(cleaned[0]))
        {
            cleaned = char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
        }

        if (!cleaned.EndsWith('.') && !cleaned.EndsWith('!') && !cleaned.EndsWith('?'))
        {
            cleaned += ".";
        }

        return cleaned;
    }

    private async Task<ArticleEnrichmentResult?> TryEnrichWithGeminiAsync(
        string title,
        string? summary,
        string? content,
        string? category,
        string apiKey,
        CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(12));

            var client = _httpClientFactory.CreateClient();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";

            var promptText = $$"""
                You are a senior news intelligence editor for Nigerian News Stand NG.
                Given the following full news article, generate:
                1. Exactly 3 concise, highly informative bullet-point Key Takeaways.
                2. 2 to 3 chronological or financial timeline facts.
                3. 2 to 3 structured FAQ question and answer pairs.
                4. A comprehensive, structured Full Summary (executiveOverview, contextAndBackground, keyFiguresAndEntities, outlookOrNextSteps, readingTimeSeconds).

                CRITICAL GUARDRAIL: Base everything STRICTLY on the text below. Never extrapolate, hallucinate, or invent quotes, names, or Naira amounts.
                Format your output STRICTLY as a JSON object matching this schema:
                {
                  "keyTakeaways": ["bullet 1", "bullet 2", "bullet 3"],
                  "timelineFacts": [
                    { "fact": "full sentence fact", "dateText": "date or amount", "entity": "optional entity name" }
                  ],
                  "faqs": [
                    { "question": "Question text?", "answer": "Factual answer." }
                  ],
                  "fullSummary": {
                    "executiveOverview": "2 to 3 sentences synthesizing what happened and the core decision.",
                    "contextAndBackground": [
                      "Key policy, economic, or legal context in Nigeria.",
                      "Implications for Nigerian citizens, businesses, or state/federal institutions."
                    ],
                    "keyFiguresAndEntities": [
                      "Named official / entity and their exact stance or quote mentioned.",
                      "Extracted numerical/financial amount (e.g. Naira ₦ or percentage) or timeline."
                    ],
                    "outlookOrNextSteps": "Immediate next steps, upcoming court dates, legislative hearings, or market impacts.",
                    "readingTimeSeconds": 45
                  }
                }

                Title: {{title}}
                Category: {{category ?? "General"}}
                Summary: {{summary ?? "N/A"}}
                Content: {{content ?? summary ?? title}}
                """;

            var payload = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = promptText } } }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    temperature = 0.2
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, jsonContent, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(responseBody);

                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var textResponse = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
                    if (!string.IsNullOrWhiteSpace(textResponse))
                    {
                        var parsed = JsonSerializer.Deserialize<GeminiEnrichmentPayload>(textResponse, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (parsed != null && parsed.KeyTakeaways.Count >= 2)
                        {
                            var fullSummary = parsed.FullSummary ?? new FullSummaryPayload
                            {
                                ExecutiveOverview = string.Join(" ", parsed.KeyTakeaways.Take(2)),
                                ContextAndBackground = parsed.KeyTakeaways.Skip(2).ToList(),
                                KeyFiguresAndEntities = parsed.TimelineFacts.Select(f => $"{f.DateText}: {f.Fact}").ToList(),
                                OutlookOrNextSteps = "Follow original publisher for developing details.",
                                ReadingTimeSeconds = 40
                            };

                            return new ArticleEnrichmentResult(
                                parsed.KeyTakeaways,
                                parsed.TimelineFacts,
                                parsed.Faqs,
                                "Gemini-1.5-Flash",
                                fullSummary);
                        }
                    }
                }
            }
            else
            {
                _logger.LogWarning("Gemini enrichment returned non-success HTTP {StatusCode} ({Reason})",
                    (int)response.StatusCode, response.ReasonPhrase);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini enrichment failed or timed out: {Message}. Falling back to local TextRank.", ex.Message);
        }

        return null;
    }

    private sealed class GeminiEnrichmentPayload
    {
        public List<string> KeyTakeaways { get; set; } = [];
        public List<TimelineFactDto> TimelineFacts { get; set; } = [];
        public List<FaqItemDto> Faqs { get; set; } = [];
        public FullSummaryPayload? FullSummary { get; set; }
    }
}
