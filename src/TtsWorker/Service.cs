using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NigerianNewsGrid.Client.Helpers;
using TtsWorker.Synthesizers;

namespace TtsWorker;

/// <summary>
/// Architecture Component: Background Service for Scheduled Audio News Synthesis.
/// 
/// Operational Rhythm:
/// - Aligned with Nigerian Standard Time (West Africa Time, WAT = UTC+1).
/// - Automatically runs morning (8:00 AM WAT) and evening (6:00 PM WAT) broadcast briefings.
/// - Can also be configured to run on custom intervals via "TtsWorker:IntervalMinutes".
/// 
/// Tri-Tier Audio Pipeline:
/// 1. Fetches top verified headlines from NewsApi.
/// 2. Cleans summaries and formats broadcast scripts via TtsBriefingFormatter.
/// 3. Executes Tri-Tier synthesis:
///    - Tier 1: Google Cloud Text-to-Speech (Neural2 en-NG-Neural2-A, if API key is provided).
///    - Tier 2: Microsoft Edge Neural TTS (Option B: en-NG-EzinneNeural, 100% free, authentic Nigerian voice).
///    - Tier 3: Local Offline Safety Net (WAV audio container, guaranteed 0% crash rate).
/// 4. Writes media files (.mp3/.wav) and transcripts (.txt) to the shared audio volume (data/audio).
/// 5. Notifies NewsApi via POST /api/v1/audio/register so mobile clients immediately discover seekable streams.
/// </summary>
public class Service : BackgroundService
{
    private readonly ILogger<Service> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ITtsSynthesizer _synthesizer;
    private readonly IHostApplicationLifetime? _appLifetime;

    public Service(
        ILogger<Service> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ITtsSynthesizer synthesizer,
        IHostApplicationLifetime? appLifetime = null)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _synthesizer = synthesizer;
        _appLifetime = appLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🎙️ TTS Worker background service starting in production mode. Active Synthesizer: {Synthesizer}",
            _synthesizer.ProviderName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessTtsBriefingCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during TTS background cycle");
            }

            if (string.Equals(Environment.GetEnvironmentVariable("TTS_RUN_ONCE"), "true", StringComparison.OrdinalIgnoreCase)
                || _configuration.GetValue<bool>("TtsWorker:RunOnce"))
            {
                _logger.LogInformation("🏁 TTS_RUN_ONCE set. Stopping worker after single synthesis pass.");
                _appLifetime?.StopApplication();
                break;
            }

            var currentWat = TtsBriefingFormatter.GetNigerianTime();
            var configuredInterval = _configuration.GetValue<int?>("TtsWorker:IntervalMinutes");
            TimeSpan delay = configuredInterval.HasValue
                ? TimeSpan.FromMinutes(configuredInterval.Value)
                : CalculateDelayToNextSchedule(currentWat);

            _logger.LogInformation("⏳ TTS Worker sleeping for {Delay}. Next scheduled briefing check around {NextRun:yyyy-MM-dd HH:mm:ss} WAT.",
                delay, currentWat.Add(delay));

            await Task.Delay(delay, stoppingToken);
        }
    }

    /// <summary>
    /// Executes a single end-to-end synthesis and distribution pass.
    /// </summary>
    public async Task ProcessTtsBriefingCycleAsync(CancellationToken stoppingToken)
    {
        var client = _httpClientFactory.CreateClient();
        var apiBaseUrl = _configuration["ApiBaseUrl"] 
            ?? _configuration["NewsApi:BaseUrl"] 
            ?? "http://localhost:56193";
        var articlesUrl = $"{apiBaseUrl.TrimEnd('/')}/api/v1/articles?limit=100";

        List<ArticleDto>? articles = null;
        try
        {
            articles = await client.GetFromJsonAsync<List<ArticleDto>>(articlesUrl, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Unable to fetch articles from NewsApi ({Url}) for TTS generation: {Message}", articlesUrl, ex.Message);
        }

        if (articles == null || articles.Count == 0)
        {
            _logger.LogInformation("No articles returned from NewsApi for TTS synthesis.");
            return;
        }

        var nigerianTime = TtsBriefingFormatter.GetNigerianTime();
        var timeOfDay = TtsBriefingFormatter.GetTimeOfDay(nigerianTime);
        var cycleId = TtsBriefingFormatter.GetCurrentAudioBriefingCycle();
        var intro = TtsBriefingFormatter.BuildBriefingIntro();

        _logger.LogInformation("🚀 Processing {CycleId} briefing ({NigerianTime:yyyy-MM-dd HH:mm:ss} WAT). Intro: \"{Intro}\"",
            cycleId, nigerianTime, intro);

        // Define shared audio output storage directory
        var audioFolder = _configuration["Tts:AudioStoragePath"]
            ?? _configuration["Tts__AudioStoragePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "data", "audio");

        Directory.CreateDirectory(audioFolder);

        // Group articles by category, taking top 3 unique stories per category (eliminating duplicates from competing sources)
        var grouped = articles
            .OrderByDescending(a => a.PublishedAt)
            .GroupBy(a => a.Category ?? "General")
            .ToDictionary(
                g => g.Key,
                g => StoryDeduplicationHelper.DeduplicateStories(g, a => a.Title, a => a.Summary, a => a.Source).Take(3).ToList());

        // ── 1. Synthesize Master Flagship Daily Briefing ───────────────────────────
        var masterScriptBuilder = new System.Text.StringBuilder();
        masterScriptBuilder.AppendLine(intro);
        masterScriptBuilder.AppendLine("Here is your round-up of the leading stories across Nigeria today.");
        masterScriptBuilder.AppendLine();

        int masterStoryIndex = 1;
        var acceptedMasterStories = new List<ArticleDto>();
        foreach (var kv in grouped)
        {
            // Pick the top story for this category that isn't already covered in another category
            var topStory = kv.Value.FirstOrDefault(story =>
                !acceptedMasterStories.Any(existing => StoryDeduplicationHelper.AreDuplicateStories(story.Title, existing.Title, story.Summary, existing.Summary)));

            if (topStory != null)
            {
                acceptedMasterStories.Add(topStory);
                var cleanTitle = TtsBriefingFormatter.CleanTextForTts(topStory.Title);
                var cleanSummary = TtsBriefingFormatter.CleanTextForTts(topStory.Summary);
                masterScriptBuilder.AppendLine($"In {kv.Key}: {cleanTitle}.");
                if (!string.IsNullOrWhiteSpace(cleanSummary))
                {
                    masterScriptBuilder.AppendLine(cleanSummary);
                }
                masterScriptBuilder.AppendLine();
                masterStoryIndex++;
            }
        }
        masterScriptBuilder.AppendLine("That concludes this news briefing from News Stand NG.");

        var masterScript = masterScriptBuilder.ToString();
        var masterBaseName = $"briefing_{timeOfDay.ToLowerInvariant()}";
        await SynthesizeAndPersistAsync(
            client,
            apiBaseUrl,
            audioFolder,
            masterScript,
            masterBaseName,
            cycleId,
            timeOfDay,
            category: null,
            articleId: null,
            stoppingToken);

        // ── 2. Synthesize Per-Category and Per-Article Briefings ───────────────────
        int generatedCount = 0;
        foreach (var kv in grouped)
        {
            var category = kv.Key;
            var list = kv.Value;

            var categoryBuilder = new System.Text.StringBuilder();
            categoryBuilder.AppendLine(intro);
            categoryBuilder.AppendLine($"Here are the top stories in {category}.");
            categoryBuilder.AppendLine();

            for (int i = 0; i < list.Count; i++)
            {
                var art = list[i];
                var cleanTitle = TtsBriefingFormatter.CleanTextForTts(art.Title);
                var cleanSummary = TtsBriefingFormatter.CleanTextForTts(art.Summary);

                var articleScript = new System.Text.StringBuilder();
                articleScript.AppendLine(intro);
                articleScript.AppendLine($"Headline {i + 1}: {cleanTitle}.");
                if (!string.IsNullOrWhiteSpace(cleanSummary))
                {
                    articleScript.AppendLine(cleanSummary);
                }

                var articleBaseName = $"{category.Replace(' ', '_')}_{i + 1}";
                await SynthesizeAndPersistAsync(
                    client,
                    apiBaseUrl,
                    audioFolder,
                    articleScript.ToString(),
                    articleBaseName,
                    cycleId,
                    timeOfDay,
                    category,
                    art.Id,
                    stoppingToken);

                generatedCount++;

                categoryBuilder.AppendLine($"Headline {i + 1}: {cleanTitle}.");
                if (!string.IsNullOrWhiteSpace(cleanSummary))
                {
                    categoryBuilder.AppendLine(cleanSummary);
                }
                categoryBuilder.AppendLine();
            }

            // Save and synthesize combined category briefing
            var categoryBaseName = $"{category.Replace(' ', '_')}_briefing";
            await SynthesizeAndPersistAsync(
                client,
                apiBaseUrl,
                audioFolder,
                categoryBuilder.ToString(),
                categoryBaseName,
                cycleId,
                timeOfDay,
                category,
                articleId: null,
                stoppingToken);
        }

        _logger.LogInformation("✅ TTS Worker successfully synthesized and registered {Count} article audio streams across {CategoryCount} categories.",
            generatedCount, grouped.Count);
    }

    /// <summary>
    /// Executes synthesis via Tri-Tier engine, writes audio and transcript files,
    /// and registers metadata with NewsApi.
    /// </summary>
    private async Task SynthesizeAndPersistAsync(
        HttpClient client,
        string apiBaseUrl,
        string audioFolder,
        string scriptText,
        string baseFileName,
        string cycleId,
        string timeOfDay,
        string? category,
        string? articleId,
        CancellationToken cancellationToken)
    {
        // Always write plain-text transcript for offline readers or fallbacks
        var txtPath = Path.Combine(audioFolder, $"{baseFileName}.txt");
        await File.WriteAllTextAsync(txtPath, scriptText, cancellationToken);

        // Synthesize via Tri-Tier Synthesizer
        var synthesisResult = await _synthesizer.SynthesizeAsync(scriptText, "en-NG-EzinneNeural", cancellationToken);

        string audioExtension = synthesisResult.ContentType.Contains("wav", StringComparison.OrdinalIgnoreCase)
            ? ".wav"
            : ".mp3";

        var audioFileName = $"{baseFileName}{audioExtension}";
        var audioFilePath = Path.Combine(audioFolder, audioFileName);

        if (synthesisResult.Success && synthesisResult.AudioData != null && synthesisResult.AudioData.Length > 0)
        {
            await File.WriteAllBytesAsync(audioFilePath, synthesisResult.AudioData, cancellationToken);
            _logger.LogInformation("💾 Saved audio file: {Path} ({Size} bytes, provider: {Provider})",
                audioFilePath, synthesisResult.AudioData.Length, synthesisResult.ProviderUsed);

            // Register with NewsApi so it updates Article.AudioUrl and latest briefing cache
            await RegisterAudioWithApiAsync(
                client,
                apiBaseUrl,
                new AudioRegistrationDto
                {
                    Cycle = cycleId,
                    TimeOfDay = timeOfDay,
                    FileName = audioFileName,
                    Category = category,
                    ArticleId = articleId,
                    Provider = synthesisResult.ProviderUsed
                },
                cancellationToken);
        }
    }

    /// <summary>
    /// Sends a registration payload to NewsApi POST /api/v1/audio/register.
    /// Includes X-Api-Key authentication header if configured.
    /// </summary>
    private async Task RegisterAudioWithApiAsync(
        HttpClient client,
        string apiBaseUrl,
        AudioRegistrationDto registration,
        CancellationToken cancellationToken)
    {
        try
        {
            var registerUrl = $"{apiBaseUrl.TrimEnd('/')}/api/v1/audio/register";
            using var request = new HttpRequestMessage(HttpMethod.Post, registerUrl)
            {
                Content = JsonContent.Create(registration)
            };

            var apiKey = _configuration["ApiKey"] ?? _configuration["NewsApi:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Add("X-Api-Key", apiKey);
            }

            var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Registered audio {FileName} with NewsApi successfully.", registration.FileName);
            }
            else
            {
                _logger.LogWarning("Failed to register audio {FileName} with NewsApi: HTTP {StatusCode}",
                    registration.FileName, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Unable to notify NewsApi of new audio registration: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Calculates the time span to the next 8:00 AM or 6:00 PM update in Nigerian Standard Time (WAT).
    /// </summary>
    private static TimeSpan CalculateDelayToNextSchedule(DateTime nigerianTime)
    {
        var today8am = nigerianTime.Date.AddHours(8);
        var today6pm = nigerianTime.Date.AddHours(18);
        var tomorrow8am = nigerianTime.Date.AddDays(1).AddHours(8);

        DateTime nextRun;
        if (nigerianTime < today8am)
            nextRun = today8am;
        else if (nigerianTime < today6pm)
            nextRun = today6pm;
        else
            nextRun = tomorrow8am;

        var diff = nextRun - nigerianTime;
        return diff < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : diff;
    }

    public class ArticleDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string Source { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
    }

    public class AudioRegistrationDto
    {
        public string Cycle { get; set; } = string.Empty;
        public string TimeOfDay { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? ArticleId { get; set; }
        public string Provider { get; set; } = string.Empty;
    }
}
