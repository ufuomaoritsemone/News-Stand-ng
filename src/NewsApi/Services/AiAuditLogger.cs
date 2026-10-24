using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NewsApi.Services;

/// <summary>
/// Thread-safe audit logger that writes every Gemini request, success, error,
/// and ML Summarizer fallback to both a formatted log file and a structured JSONL file.
/// </summary>
public sealed class AiAuditLogger : IAiAuditLogger, IDisposable
{
    private readonly ILogger<AiAuditLogger> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string LogFilePath { get; }
    public string JsonlFilePath { get; }

    public AiAuditLogger(IConfiguration config, ILogger<AiAuditLogger> logger)
    {
        _logger = logger;

        var configuredPath = config["AiEnrichment:AuditLogPath"]
            ?? config["AiEnrichment:LogFilePath"]
            ?? Path.Combine("logs", "gemini_ai_audit.log");

        // Resolve absolute directory
        var fullLogPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));

        var logDirectory = Path.GetDirectoryName(fullLogPath)
            ?? Path.Combine(Directory.GetCurrentDirectory(), "logs");

        Directory.CreateDirectory(logDirectory);

        LogFilePath = fullLogPath;
        JsonlFilePath = Path.ChangeExtension(fullLogPath, ".jsonl");

        _logger.LogInformation("AiAuditLogger initialized.\n  Formatted Log : {LogFile}\n  JSONL Log     : {JsonlFile}",
            LogFilePath, JsonlFilePath);
    }

    public async Task LogGeminiRequestAsync(
        string? articleId,
        string title,
        string model,
        string? promptPreview,
        int promptLength,
        double temperature,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTime.UtcNow;
        var entry = new
        {
            Timestamp = timestamp,
            EventType = "GEMINI_REQUEST",
            ArticleId = articleId,
            Title = title,
            Model = model,
            PromptLength = promptLength,
            Temperature = temperature,
            PromptPreview = promptPreview
        };

        var humanLog = $"""
        [{timestamp:yyyy-MM-dd HH:mm:ss.fff UTC}] 🤖 [GEMINI_REQUEST]
          Article   : '{title}' (Id: {articleId ?? "N/A"})
          Model     : {model} | Temperature: {temperature} | Prompt Length: {promptLength:N0} chars
          Preview   : {promptPreview?.Replace("\n", " ").Trim()}

        """;

        await AppendAsync(humanLog, entry, cancellationToken);
    }

    public async Task LogGeminiSuccessAsync(
        string? articleId,
        string title,
        string model,
        long elapsedMilliseconds,
        int takeawaysCount,
        int timelineCount,
        int faqsCount,
        int readingTimeSeconds,
        string? overviewPreview,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTime.UtcNow;
        var entry = new
        {
            Timestamp = timestamp,
            EventType = "GEMINI_SUCCESS",
            ArticleId = articleId,
            Title = title,
            Model = model,
            ElapsedMilliseconds = elapsedMilliseconds,
            TakeawaysCount = takeawaysCount,
            TimelineCount = timelineCount,
            FaqsCount = faqsCount,
            ReadingTimeSeconds = readingTimeSeconds,
            ExecutiveOverview = overviewPreview
        };

        var humanLog = $"""
        [{timestamp:yyyy-MM-dd HH:mm:ss.fff UTC}] ✨ [GEMINI_SUCCESS] ({elapsedMilliseconds:N0} ms)
          Article   : '{title}' (Id: {articleId ?? "N/A"})
          Model     : {model}
          Output    : Takeaways: {takeawaysCount} | Timeline Facts: {timelineCount} | FAQs: {faqsCount} | Reading Time: {readingTimeSeconds}s
          Overview  : {overviewPreview?.Replace("\n", " ").Trim()}

        """;

        await AppendAsync(humanLog, entry, cancellationToken);
    }

    public async Task LogGeminiErrorAsync(
        string? articleId,
        string title,
        string model,
        long elapsedMilliseconds,
        int? httpStatusCode,
        string? reasonPhrase,
        string? responseBody,
        Exception? exception,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTime.UtcNow;
        var entry = new
        {
            Timestamp = timestamp,
            EventType = "GEMINI_ERROR",
            ArticleId = articleId,
            Title = title,
            Model = model,
            ElapsedMilliseconds = elapsedMilliseconds,
            HttpStatusCode = httpStatusCode,
            ReasonPhrase = reasonPhrase,
            ResponseBody = responseBody,
            ExceptionType = exception?.GetType().FullName,
            ExceptionMessage = exception?.Message,
            StackTrace = exception?.StackTrace
        };

        var exDetail = exception != null
            ? (exception.InnerException != null
                ? $"{exception.GetType().Name}: {exception.Message} ({exception.InnerException.GetType().Name}: {exception.InnerException.Message})"
                : $"{exception.GetType().Name}: {exception.Message}")
            : null;

        var errorSummary = httpStatusCode.HasValue
            ? $"HTTP {httpStatusCode} {reasonPhrase}"
            : exDetail ?? "Unknown Error";

        var humanLog = $"""
        [{timestamp:yyyy-MM-dd HH:mm:ss.fff UTC}] ❌ [GEMINI_ERROR] ({elapsedMilliseconds:N0} ms)
          Article   : '{title}' (Id: {articleId ?? "N/A"})
          Model     : {model}
          Status    : {errorSummary}
          Body      : {responseBody?.Trim()}
          Exception : {(exception?.InnerException != null ? $"{exception.Message} -> {exception.InnerException.Message}" : exception?.Message)}

        """;

        await AppendAsync(humanLog, entry, cancellationToken);
    }

    public async Task LogCircuitBreakerTrippedAsync(
        string reason,
        DateTime cooldownUntilUtc,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTime.UtcNow;
        var entry = new
        {
            Timestamp = timestamp,
            EventType = "CIRCUIT_BREAKER_TRIPPED",
            Reason = reason,
            CooldownUntilUtc = cooldownUntilUtc,
            CooldownMinutes = (cooldownUntilUtc - timestamp).TotalMinutes
        };

        var humanLog = $"""
        [{timestamp:yyyy-MM-dd HH:mm:ss.fff UTC}] ⚡ [CIRCUIT_BREAKER_TRIPPED]
          Reason    : {reason}
          Cooldown  : Active until {cooldownUntilUtc:yyyy-MM-dd HH:mm:ss.fff UTC} (10 min bypass)

        """;

        await AppendAsync(humanLog, entry, cancellationToken);
    }

    public async Task LogMlSummarizerEngagedAsync(
        string? articleId,
        string title,
        string? category,
        string engagementReason,
        int sentenceCount,
        long elapsedMilliseconds,
        int takeawaysCount,
        string? overviewPreview,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTime.UtcNow;
        var entry = new
        {
            Timestamp = timestamp,
            EventType = "ML_SUMMARIZER_ENGAGED",
            Engine = "TextRank-Extractive (MMR Centroid)",
            ArticleId = articleId,
            Title = title,
            Category = category,
            EngagementReason = engagementReason,
            CandidateSentences = sentenceCount,
            ElapsedMilliseconds = elapsedMilliseconds,
            TakeawaysCount = takeawaysCount,
            ExecutiveOverview = overviewPreview
        };

        var humanLog = $"""
        [{timestamp:yyyy-MM-dd HH:mm:ss.fff UTC}] 📌 [ML_SUMMARIZER_ENGAGED] ({elapsedMilliseconds:N0} ms)
          Engine    : TextRank-Extractive (MMR Centroid)
          Article   : '{title}' (Id: {articleId ?? "N/A"} | Category: {category ?? "General"})
          Reason    : {engagementReason}
          Stats     : Sentences: {sentenceCount} | Selected Takeaways: {takeawaysCount}
          Overview  : {overviewPreview?.Replace("\n", " ").Trim()}

        """;

        await AppendAsync(humanLog, entry, cancellationToken);
    }

    private async Task AppendAsync(string humanReadableText, object jsonObject, CancellationToken ct)
    {
        try
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                // 1. Append formatted human-readable block to .log
                await File.AppendAllTextAsync(LogFilePath, humanReadableText, System.Text.Encoding.UTF8, ct);

                // 2. Append single-line JSON to .jsonl
                var jsonLine = JsonSerializer.Serialize(jsonObject, _jsonOptions) + Environment.NewLine;
                await File.AppendAllTextAsync(JsonlFilePath, jsonLine, System.Text.Encoding.UTF8, ct);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write AI audit log to {LogFilePath}", LogFilePath);
        }
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
