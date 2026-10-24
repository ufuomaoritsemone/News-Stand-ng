using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsApi.Data;
using NewsApi.Services;

namespace NewsApi.Controllers;

[ApiController]
[Route("[controller]")]
[Route("api/v1/[controller]")]
public class HealthController(
    NewsDbContext db,
    IWebHostEnvironment env,
    IConfiguration config,
    ISystemSettingsService settingsService,
    ILogger<HealthController> logger,
    IArticleEnrichmentService? enrichmentService = null,
    IAiAuditLogger? auditLogger = null) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });

    [HttpGet("details")]
    public async Task<IActionResult> GetDetailedHealth(CancellationToken ct = default)
    {
        var proc = Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow - proc.StartTime.ToUniversalTime();

        // 1. Database Connectivity & Ping Latency
        var sw = Stopwatch.StartNew();
        bool dbConnected = false;
        string? dbError = null;
        try
        {
            dbConnected = await db.Database.CanConnectAsync(ct);
            sw.Stop();
        }
        catch (Exception ex)
        {
            sw.Stop();
            dbError = ex.Message;
        }

        var isPostgres = db.Database.ProviderName?.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true;
        var dbProvider = isPostgres ? "PostgreSQL" : "SQLite";

        // 2. Database Entities Counts
        int totalArticles = 0;
        int totalSources = 0;
        int totalComments = 0;
        int totalFeedbacks = 0;
        DateTime? latestArticleAt = null;
        string? latestArticleTitle = null;

        if (dbConnected)
        {
            try
            {
                totalArticles = await db.Articles.CountAsync(ct);
                totalSources = await db.Sources.CountAsync(ct);
                totalComments = await db.StoryComments.CountAsync(ct);
                totalFeedbacks = await db.Feedbacks.CountAsync(ct);

                var latest = await db.Articles
                    .OrderByDescending(a => a.PublishedAt)
                    .Select(a => new { a.PublishedAt, a.Title })
                    .FirstOrDefaultAsync(ct);

                if (latest != null)
                {
                    latestArticleAt = latest.PublishedAt;
                    latestArticleTitle = latest.Title;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch entity counts for health check");
            }
        }

        // 3. Background Scraper Evaluation
        string scraperStatus = "Unknown";
        if (latestArticleAt.HasValue)
        {
            var diff = DateTime.UtcNow - latestArticleAt.Value.ToUniversalTime();
            if (diff.TotalMinutes <= 60)
                scraperStatus = "Active & Ingesting";
            else if (diff.TotalHours <= 6)
                scraperStatus = "Idle / Normal Ingestion Gap";
            else
                scraperStatus = "Attention Needed (Stalled Ingestion)";
        }
        else
        {
            scraperStatus = "No Articles Ingested Yet";
        }

        // 4. Market Data Tracker Status
        DateTime? fxLastUpdated = null;
        try
        {
            var fxRecord = await db.MarketDataRecords.FirstOrDefaultAsync(m => m.MetricKey == "naira-fx", ct);
            if (fxRecord != null)
            {
                fxLastUpdated = fxRecord.LastUpdated;
            }
        }
        catch { }

        // 5. Storage Inspection
        var imgCacheDir = Path.Combine(env.ContentRootPath, "data", "img_cache");
        int imgCacheFiles = 0;
        long imgCacheSizeBytes = 0;
        if (Directory.Exists(imgCacheDir))
        {
            try
            {
                var files = new DirectoryInfo(imgCacheDir).GetFiles("*.webp");
                imgCacheFiles = files.Length;
                imgCacheSizeBytes = files.Sum(f => f.Length);
            }
            catch { }
        }

        var audioDir = config["Tts:AudioStoragePath"] 
            ?? config["Tts__AudioStoragePath"] 
            ?? Path.Combine(AppContext.BaseDirectory, "data", "audio");
        int audioFiles = 0;
        long audioSizeBytes = 0;
        if (Directory.Exists(audioDir))
        {
            try
            {
                var files = new DirectoryInfo(audioDir).GetFiles("*.mp3");
                audioFiles = files.Length;
                audioSizeBytes = files.Sum(f => f.Length);
            }
            catch { }
        }

        var overallStatus = (dbConnected && (scraperStatus == "Active & Ingesting" || scraperStatus == "Idle / Normal Ingestion Gap"))
            ? "Healthy"
            : (!dbConnected ? "Unhealthy" : "Degraded");

        bool cloudAiAvailable = false;
        bool circuitBreakerActive = false;
        DateTime? circuitBreakerUntilUtc = null;
        if (enrichmentService != null)
        {
            cloudAiAvailable = await enrichmentService.IsCloudAiAvailableAsync(ct);
            circuitBreakerActive = enrichmentService.IsCircuitBreakerActive;
            circuitBreakerUntilUtc = enrichmentService.CircuitBreakerUntilUtc;
        }

        var response = new
        {
            status = overallStatus,
            serverTimeUtc = DateTimeOffset.UtcNow,
            environment = env.EnvironmentName,
            process = new
            {
                processId = proc.Id,
                uptimeSeconds = (long)uptime.TotalSeconds,
                uptimeFormatted = $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s",
                startTimeUtc = proc.StartTime.ToUniversalTime(),
                workingSetMb = Math.Round((double)proc.WorkingSet64 / 1024 / 1024, 2),
                gcAllocatedMb = Math.Round((double)GC.GetTotalMemory(false) / 1024 / 1024, 2),
                dotNetVersion = Environment.Version.ToString(),
                osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()
            },
            database = new
            {
                provider = dbProvider,
                connected = dbConnected,
                error = dbError,
                latencyMs = sw.ElapsedMilliseconds,
                totalArticles,
                totalSources,
                totalComments,
                totalFeedbacks
            },
            aiEnrichment = new
            {
                configured = cloudAiAvailable || circuitBreakerActive,
                cloudAiAvailable,
                circuitBreakerActive,
                circuitBreakerUntilUtc,
                model = await settingsService.GetSettingAsync("Gemini:Model",
                    Environment.GetEnvironmentVariable("GEMINI_MODEL")
                    ?? config["Gemini:Model"]
                    ?? config["Gemini__Model"]
                    ?? "gemini-3.1-flash-lite", ct),
                fallbackEngine = "TextRank-Extractive",
                auditLog = auditLogger != null ? new
                {
                    logPath = auditLogger.LogFilePath,
                    jsonlPath = auditLogger.JsonlFilePath,
                    logExists = System.IO.File.Exists(auditLogger.LogFilePath),
                    jsonlExists = System.IO.File.Exists(auditLogger.JsonlFilePath)
                } : null
            },
            workers = new
            {
                scraperStatus,
                latestArticleAt,
                latestArticleTitle,
                fxLastUpdated
            },
            storage = new
            {
                imageProxyCache = new
                {
                    fileCount = imgCacheFiles,
                    sizeMb = Math.Round((double)imgCacheSizeBytes / 1024 / 1024, 2),
                    directory = imgCacheDir
                },
                audioStorage = new
                {
                    fileCount = audioFiles,
                    sizeMb = Math.Round((double)audioSizeBytes / 1024 / 1024, 2),
                    directory = audioDir
                }
            }
        };

        return Ok(response);
    }

    [HttpPost("purge-cache")]
    public IActionResult PurgeCache()
    {
        int deletedFiles = 0;
        long freedBytes = 0;

        var imgCacheDir = Path.Combine(env.ContentRootPath, "data", "img_cache");
        if (Directory.Exists(imgCacheDir))
        {
            try
            {
                var files = new DirectoryInfo(imgCacheDir).GetFiles("*.webp");
                foreach (var file in files)
                {
                    try
                    {
                        freedBytes += file.Length;
                        file.Delete();
                        deletedFiles++;
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while purging image cache directory.");
            }
        }

        settingsService.InvalidateCache();

        return Ok(new
        {
            success = true,
            message = $"Successfully purged {deletedFiles} image cache files ({Math.Round((double)freedBytes / 1024 / 1024, 2)} MB) and flushed settings cache.",
            filesDeleted = deletedFiles,
            freedMb = Math.Round((double)freedBytes / 1024 / 1024, 2)
        });
    }

    [HttpPost("ping-db")]
    public async Task<IActionResult> PingDatabase(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var canConnect = await db.Database.CanConnectAsync(ct);
            sw.Stop();
            return Ok(new
            {
                success = canConnect,
                latencyMs = sw.ElapsedMilliseconds,
                provider = db.Database.ProviderName,
                message = canConnect ? $"Database response received in {sw.ElapsedMilliseconds} ms." : "Database connection failed."
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Ok(new
            {
                success = false,
                latencyMs = sw.ElapsedMilliseconds,
                error = ex.Message
            });
        }
    }
}
