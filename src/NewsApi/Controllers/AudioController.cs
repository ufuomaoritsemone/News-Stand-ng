using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NewsApi.Data;
using NewsApi.Models;

namespace NewsApi.Controllers;

/// <summary>
/// Architecture Component: Audio Delivery & Seekable Streaming Controller.
/// 
/// Purpose:
/// Delivers high-definition news briefings and article audio streams synthesized by TtsWorker.
/// 
/// Key Features:
/// 1. HTTP 206 Partial Content (Range Requests):
///    Enables seekable, buffered audio playback on mobile devices (MAUI, iOS AVPlayer, Android ExoPlayer)
///    via PhysicalFile(..., enableRangeProcessing: true). Mobile devices can pause, resume, and jump
///    to any timestamp without redownloading the entire stream.
/// 2. Automatic Registration:
///    Receives audio registration from TtsWorker and directly attaches stream URLs to database Article records.
/// 3. Offline Safety & Cache Discovery:
///    Tracks current morning/evening briefing cycles and informs client apps of audio availability.
/// 4. Path Traversal Hardening:
///    Sanitizes all incoming file names using Path.GetFileName to ensure requests cannot escape data/audio.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class AudioController : ControllerBase
{
    private readonly NewsDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AudioController> _logger;

    private const string LatestBriefingCacheKey = "audio_briefing_latest";
    private static readonly HashSet<string> AllowedAudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".m4a", ".ogg", ".aac"
    };
    private const long MaxAudioSizeBytes = 50 * 1024 * 1024; // 50 MB

    public AudioController(
        NewsDbContext db,
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<AudioController> logger)
    {
        _db = db;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Returns discovery metadata for the latest audio briefing (morning or evening WAT).
    /// </summary>
    [HttpGet("briefings/latest")]
    public IActionResult GetLatestBriefing()
    {
        var audioFolder = GetAudioStorageFolder();

        // 1. Check in-memory cached registration
        if (_cache.TryGetValue<AudioBriefingMetadata>(LatestBriefingCacheKey, out var cached) && cached != null)
        {
            var cachedFilePath = Path.Combine(audioFolder, cached.FileName);
            if (System.IO.File.Exists(cachedFilePath))
            {
                return Ok(cached with { Available = true });
            }
        }

        // 2. Discover from disk if available
        var (morningExists, morningFile) = FindAudioFile(audioFolder, "briefing_morning");
        var (eveningExists, eveningFile) = FindAudioFile(audioFolder, "briefing_evening");

        if (eveningExists && eveningFile != null)
        {
            var info = new FileInfo(Path.Combine(audioFolder, eveningFile));
            var metadata = new AudioBriefingMetadata(
                Cycle: $"{DateTime.UtcNow:yyyy-MM-dd}_evening",
                TimeOfDay: "evening",
                FileName: eveningFile,
                StreamUrl: $"/api/v1/audio/{eveningFile}",
                PublishedAt: info.LastWriteTimeUtc,
                Provider: "TriTierNeural",
                Available: true
            );
            _cache.Set(LatestBriefingCacheKey, metadata, TimeSpan.FromMinutes(30));
            return Ok(metadata);
        }

        if (morningExists && morningFile != null)
        {
            var info = new FileInfo(Path.Combine(audioFolder, morningFile));
            var metadata = new AudioBriefingMetadata(
                Cycle: $"{DateTime.UtcNow:yyyy-MM-dd}_morning",
                TimeOfDay: "morning",
                FileName: morningFile,
                StreamUrl: $"/api/v1/audio/{morningFile}",
                PublishedAt: info.LastWriteTimeUtc,
                Provider: "TriTierNeural",
                Available: true
            );
            _cache.Set(LatestBriefingCacheKey, metadata, TimeSpan.FromMinutes(30));
            return Ok(metadata);
        }

        return Ok(new AudioBriefingMetadata(
            Cycle: string.Empty,
            TimeOfDay: string.Empty,
            FileName: string.Empty,
            StreamUrl: string.Empty,
            PublishedAt: DateTime.UtcNow,
            Provider: "None",
            Available: false
        ));
    }

    /// <summary>
    /// Streams the requested audio file with full HTTP Range (206 Partial Content) support.
    /// Mobile players utilize Range headers (e.g. bytes=0-1024) to stream and seek through audio without full downloads.
    /// </summary>
    [HttpGet("{fileName}")]
    public IActionResult StreamAudio(string fileName)
    {
        var sanitizedFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(sanitizedFileName) || sanitizedFileName != fileName)
        {
            return BadRequest(new { error = "Invalid file name parameter." });
        }

        var audioFolder = GetAudioStorageFolder();
        var filePath = Path.Combine(audioFolder, sanitizedFileName);

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(new { error = $"Audio file '{sanitizedFileName}' was not found on this server." });
        }

        var contentType = GetContentType(sanitizedFileName);

        // Fix #3: enableRangeProcessing: true enables RFC 7233 HTTP 206 Partial Content streaming
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// Registers newly generated audio assets produced by TtsWorker.
    /// If an ArticleId is provided, links the stream URL to the Article.AudioUrl property in PostgreSQL/SQLite.
    /// Protected endpoint (requires X-Api-Key if configured).
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAudio(
        [FromBody] AudioRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.FileName))
        {
            return BadRequest(new { error = "FileName is required." });
        }

        var sanitizedFileName = Path.GetFileName(request.FileName);
        var streamUrl = $"/api/v1/audio/{sanitizedFileName}";

        // If this represents a global daily briefing, update discovery cache
        if (sanitizedFileName.StartsWith("briefing_", StringComparison.OrdinalIgnoreCase))
        {
            var metadata = new AudioBriefingMetadata(
                Cycle: request.Cycle,
                TimeOfDay: request.TimeOfDay,
                FileName: sanitizedFileName,
                StreamUrl: streamUrl,
                PublishedAt: DateTime.UtcNow,
                Provider: request.Provider,
                Available: true
            );
            _cache.Set(LatestBriefingCacheKey, metadata, TimeSpan.FromHours(6));
            _logger.LogInformation("Updated latest briefing cache to '{FileName}' ({Provider}).", sanitizedFileName, request.Provider);
        }

        // If an ArticleId is attached, update the database record
        if (!string.IsNullOrWhiteSpace(request.ArticleId))
        {
            var article = await _db.Articles.FirstOrDefaultAsync(a => a.Id == request.ArticleId, cancellationToken);
            if (article != null)
            {
                article.AudioUrl = streamUrl;
                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Attached AudioUrl '{StreamUrl}' to Article '{Id}' ({Title}).",
                    streamUrl, article.Id, article.Title);
            }
        }

        return Ok(new
        {
            success = true,
            fileName = sanitizedFileName,
            streamUrl
        });
    }

    /// <summary>
    /// Direct multipart audio upload endpoint for multi-host deployments without shared file volumes.
    /// Protected endpoint (requires X-Api-Key if configured).
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(52_428_800)] // 50 MB
    public async Task<IActionResult> UploadAudio(
        [FromForm] IFormFile file,
        [FromForm] string? articleId = null,
        [FromForm] string? cycle = null,
        [FromForm] string? timeOfDay = null,
        [FromForm] string? provider = null,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded or file is empty." });
        }

        if (file.Length > MaxAudioSizeBytes)
        {
            return BadRequest(new { error = $"File exceeds maximum permitted limit of {MaxAudioSizeBytes / (1024 * 1024)} MB." });
        }

        var sanitizedFileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(sanitizedFileName) || sanitizedFileName != file.FileName)
        {
            return BadRequest(new { error = "Invalid file name parameter." });
        }

        var ext = Path.GetExtension(sanitizedFileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedAudioExtensions.Contains(ext))
        {
            return BadRequest(new { error = $"File extension '{ext}' is not permitted. Allowed extensions: {string.Join(", ", AllowedAudioExtensions)}." });
        }

        var audioFolder = GetAudioStorageFolder();
        var destinationPath = Path.Combine(audioFolder, sanitizedFileName);

        using (var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        _logger.LogInformation("Uploaded audio asset '{FileName}' ({Length} bytes) to {Path}",
            sanitizedFileName, file.Length, destinationPath);

        // Chain into registration logic
        return await RegisterAudio(new AudioRegistrationRequest(
            Cycle: cycle ?? $"{DateTime.UtcNow:yyyy-MM-dd}",
            TimeOfDay: timeOfDay ?? "daily",
            FileName: sanitizedFileName,
            Category: null,
            ArticleId: articleId,
            Provider: provider ?? "Uploaded"
        ), cancellationToken);
    }

    private string GetAudioStorageFolder()
    {
        var folder = _configuration["Tts:AudioStoragePath"]
            ?? _configuration["Tts__AudioStoragePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "data", "audio");

        Directory.CreateDirectory(folder);
        return folder;
    }

    private static (bool exists, string? fileName) FindAudioFile(string folder, string baseName)
    {
        var mp3 = $"{baseName}.mp3";
        if (System.IO.File.Exists(Path.Combine(folder, mp3))) return (true, mp3);

        var wav = $"{baseName}.wav";
        if (System.IO.File.Exists(Path.Combine(folder, wav))) return (true, wav);

        return (false, null);
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".txt" => "text/plain; charset=utf-8",
            _ => "application/octet-stream"
        };
    }

    public sealed record AudioBriefingMetadata(
        string Cycle,
        string TimeOfDay,
        string FileName,
        string StreamUrl,
        DateTime PublishedAt,
        string Provider,
        bool Available
    );

    public sealed record AudioRegistrationRequest(
        string Cycle,
        string TimeOfDay,
        string FileName,
        string? Category,
        string? ArticleId,
        string Provider
    );
}
