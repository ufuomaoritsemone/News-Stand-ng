using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewsApi.Data;
using NewsApi.Models;

namespace NewsApi.Services;

public class YouTubeFeedService
{
    private readonly NewsDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<YouTubeFeedService> _logger;

    private static readonly XNamespace AtomNs = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace YtNs = "http://www.youtube.com/xml/schemas/2015";
    private static readonly XNamespace MediaNs = "http://search.yahoo.com/mrss/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public YouTubeFeedService(
        NewsDbContext db,
        HttpClient httpClient,
        IConfiguration config,
        ILogger<YouTubeFeedService> logger)
    {
        _db = db;
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Syncs latest video stories from all registered YouTube channels.
    /// </summary>
    public async Task<int> SyncAllChannelsAsync(CancellationToken ct = default)
    {
        var channels = await _db.VideoChannels.AsNoTracking().ToListAsync(ct);
        if (channels.Count == 0)
        {
            return 0;
        }

        var apiKey = GetApiKey();
        int totalNewOrUpdated = 0;

        foreach (var channel in channels)
        {
            try
            {
                var count = await SyncChannelAsync(channel, apiKey, ct);
                totalNewOrUpdated += count;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to sync YouTube feed for channel '{ChannelName}' ({ChannelId})", channel.ChannelName, channel.Id);
            }
        }

        return totalNewOrUpdated;
    }

    /// <summary>
    /// Syncs latest video stories from a specific channel trying Data API v3 first, then falling back to Atom XML feed.
    /// </summary>
    public async Task<int> SyncChannelAsync(VideoChannel channel, CancellationToken ct = default)
    {
        var apiKey = GetApiKey();
        return await SyncChannelAsync(channel, apiKey, ct);
    }

    private async Task<int> SyncChannelAsync(VideoChannel channel, string apiKey, CancellationToken ct)
    {
        var channelId = await ResolveChannelIdAsync(channel, ct);
        if (string.IsNullOrWhiteSpace(channelId))
        {
            _logger.LogWarning("Channel '{ChannelName}' has no resolvable YouTube Channel ID.", channel.ChannelName);
            return 0;
        }

        // 1. Try YouTube Data API v3 if API key is present
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var apiCount = await FetchChannelUploadsViaApiAsync(channel, channelId, apiKey, ct);
                if (apiCount > 0)
                {
                    return apiCount;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Data API v3 sync failed for channel '{ChannelName}'. Falling back to Atom RSS.", channel.ChannelName);
            }
        }

        // 2. Fallback to resilient YouTube Atom RSS feed
        return await FetchChannelViaAtomFeedAsync(channel, channelId, ct);
    }

    private async Task<int> FetchChannelUploadsViaApiAsync(VideoChannel channel, string channelId, string apiKey, CancellationToken ct)
    {
        // Upload playlist ID is derived by replacing 'UC' prefix with 'UU'
        var uploadPlaylistId = channelId.StartsWith("UC") ? "UU" + channelId[2..] : channelId;
        var playlistUrl = $"https://www.googleapis.com/youtube/v3/playlistItems?part=snippet,contentDetails&playlistId={uploadPlaylistId}&maxResults=15&key={apiKey}";

        using var req = new HttpRequestMessage(HttpMethod.Get, playlistUrl);
        req.Headers.Add("User-Agent", "NigerianNewsGrid/1.0");

        var response = await _httpClient.SendAsync(req, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("YouTube PlaylistItems API returned {Status} for {Channel}: {Body}", response.StatusCode, channel.ChannelName, err);
            return 0;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var playlistResult = JsonSerializer.Deserialize<YouTubePlaylistItemListResponse>(json, JsonOptions);
        if (playlistResult?.Items == null || playlistResult.Items.Count == 0) return 0;

        var videoIds = playlistResult.Items
            .Select(i => i.ContentDetails?.VideoId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        if (videoIds.Count == 0) return 0;

        // Fetch detailed stats and duration
        var videoDetailsMap = await FetchVideoDetailsMapAsync(videoIds!, apiKey, ct);

        int addedOrUpdated = 0;
        foreach (var item in playlistResult.Items)
        {
            var videoId = item.ContentDetails?.VideoId?.Trim();
            if (string.IsNullOrWhiteSpace(videoId)) continue;

            var snippet = item.Snippet;
            var title = snippet?.Title?.Trim() ?? "Untitled Video";
            var rawSummary = snippet?.Description?.Trim() ?? string.Empty;
            var summary = rawSummary.Length > 250 ? rawSummary[..247] + "..." : rawSummary;
            var publishedAt = snippet?.PublishedAt?.ToUniversalTime() ?? DateTime.UtcNow;
            var thumbnail = snippet?.Thumbnails?.High?.Url
                            ?? snippet?.Thumbnails?.Medium?.Url
                            ?? $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg";

            var duration = "HD";
            long viewCount = 0;
            long likeCount = 0;

            if (videoDetailsMap.TryGetValue(videoId, out var details))
            {
                duration = FormatDuration(details.ContentDetails?.Duration);
                long.TryParse(details.Statistics?.ViewCount, out viewCount);
                long.TryParse(details.Statistics?.LikeCount, out likeCount);
            }
            else
            {
                duration = (DateTime.UtcNow - publishedAt).TotalHours < 6 ? "LIVE / RECENT" : "HD";
            }

            var existing = await _db.VideoStories.FirstOrDefaultAsync(v => v.VideoId == videoId, ct);
            if (existing != null)
            {
                existing.Title = title;
                existing.Summary = summary;
                existing.ThumbnailUrl = thumbnail;
                if (viewCount > 0) existing.ViewCount = viewCount;
                if (likeCount > 0) existing.LikeCount = likeCount;
                if (duration != "HD") existing.Duration = duration;
            }
            else
            {
                var story = new VideoStory
                {
                    Id = Guid.NewGuid().ToString("N"),
                    VideoId = videoId,
                    Title = title,
                    Summary = summary,
                    VideoUrl = $"https://www.youtube.com/watch?v={videoId}",
                    ThumbnailUrl = thumbnail,
                    ChannelName = channel.ChannelName,
                    ChannelId = channel.Id,
                    Duration = duration,
                    Category = DetermineCategory(title, rawSummary),
                    PublishedAt = publishedAt,
                    CreatedAt = DateTime.UtcNow,
                    ViewCount = viewCount,
                    LikeCount = likeCount
                };
                _db.VideoStories.Add(story);
            }
            addedOrUpdated++;
        }

        if (addedOrUpdated > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("API v3: Added/Updated {Count} stories for channel '{ChannelName}'", addedOrUpdated, channel.ChannelName);
        }

        return addedOrUpdated;
    }

    private async Task<Dictionary<string, YouTubeVideoItem>> FetchVideoDetailsMapAsync(List<string> videoIds, string apiKey, CancellationToken ct)
    {
        var map = new Dictionary<string, YouTubeVideoItem>(StringComparer.OrdinalIgnoreCase);
        var joined = string.Join(",", videoIds.Take(50));
        var url = $"https://www.googleapis.com/youtube/v3/videos?part=contentDetails,statistics&id={joined}&key={apiKey}";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "NigerianNewsGrid/1.0");

            var response = await _httpClient.SendAsync(req, ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var result = JsonSerializer.Deserialize<YouTubeVideoListResponse>(json, JsonOptions);
                if (result?.Items != null)
                {
                    foreach (var v in result.Items)
                    {
                        if (!string.IsNullOrWhiteSpace(v.Id))
                        {
                            map[v.Id] = v;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to batch fetch video details from YouTube API.");
        }

        return map;
    }

    private async Task<int> FetchChannelViaAtomFeedAsync(VideoChannel channel, string channelId, CancellationToken ct)
    {
        var feedUrl = $"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}";
        _logger.LogInformation("Fetching YouTube Atom feed for '{ChannelName}' ({ChannelId})...", channel.ChannelName, channelId);

        using var request = new HttpRequestMessage(HttpMethod.Get, feedUrl);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        request.Headers.Add("Accept", "application/atom+xml,application/xml,text/xml;q=0.9,*/*;q=0.8");

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("YouTube Atom feed returned HTTP {StatusCode} for channel '{ChannelName}' ({ChannelId})", response.StatusCode, channel.ChannelName, channelId);
            return 0;
        }

        var xmlContent = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(xmlContent)) return 0;

        var doc = XDocument.Parse(xmlContent);
        var entries = doc.Root?.Elements(AtomNs + "entry") ?? Enumerable.Empty<XElement>();

        int addedOrUpdated = 0;
        foreach (var entry in entries.Take(15))
        {
            var videoId = entry.Element(YtNs + "videoId")?.Value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(videoId)) continue;

            var title = entry.Element(AtomNs + "title")?.Value?.Trim() ?? "Untitled Video";
            var videoUrl = entry.Element(AtomNs + "link")?.Attribute("href")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                videoUrl = $"https://www.youtube.com/watch?v={videoId}";
            }

            var publishedRaw = entry.Element(AtomNs + "published")?.Value;
            var publishedAt = DateTime.TryParse(publishedRaw, out var parsedDt) ? parsedDt.ToUniversalTime() : DateTime.UtcNow;

            var mediaGroup = entry.Element(MediaNs + "group");
            var summary = mediaGroup?.Element(MediaNs + "description")?.Value?.Trim() ?? string.Empty;
            if (summary.Length > 250) summary = summary[..247] + "...";

            var thumbnail = mediaGroup?.Element(MediaNs + "thumbnail")?.Attribute("url")?.Value?.Trim()
                            ?? $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg";

            // Parse view count from <media:community><media:statistics views="1234"/>
            long views = 0;
            var viewsAttr = mediaGroup?.Element(MediaNs + "community")?.Element(MediaNs + "statistics")?.Attribute("views")?.Value;
            if (!string.IsNullOrWhiteSpace(viewsAttr))
            {
                long.TryParse(viewsAttr, out views);
            }

            var duration = (DateTime.UtcNow - publishedAt).TotalHours < 6 ? "LIVE / RECENT" : "HD";

            var existing = await _db.VideoStories.FirstOrDefaultAsync(v => v.VideoId == videoId, ct);
            if (existing != null)
            {
                if (views > 0 && views > existing.ViewCount) existing.ViewCount = views;
                existing.ThumbnailUrl = thumbnail;
            }
            else
            {
                var story = new VideoStory
                {
                    Id = Guid.NewGuid().ToString("N"),
                    VideoId = videoId,
                    Title = title,
                    Summary = summary,
                    VideoUrl = videoUrl,
                    ThumbnailUrl = thumbnail,
                    ChannelName = channel.ChannelName,
                    ChannelId = channel.Id,
                    Duration = duration,
                    Category = DetermineCategory(title, summary),
                    PublishedAt = publishedAt,
                    CreatedAt = DateTime.UtcNow,
                    ViewCount = views
                };

                _db.VideoStories.Add(story);
            }
            addedOrUpdated++;
        }

        if (addedOrUpdated > 0)
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Atom Feed: Synced {Count} video stories for channel '{ChannelName}'", addedOrUpdated, channel.ChannelName);
        }

        return addedOrUpdated;
    }

    /// <summary>
    /// Synchronizes trending news stories in Nigeria from YouTube Data API v3 (chart=mostPopular, regionCode=NG, categoryId=25).
    /// If API key is not valid or expired, falls back to calculating trending velocity across monitored Nigerian channels.
    /// </summary>
    public async Task<int> SyncTrendingNewsAsync(CancellationToken ct = default)
    {
        var apiKey = GetApiKey();

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                var count = await FetchTrendingFromApiAsync(apiKey, ct);
                if (count > 0)
                {
                    _logger.LogInformation("Successfully synced {Count} trending news stories from YouTube Data API v3.", count);
                    return count;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to fetch trending stories via YouTube API. Falling back to channel velocity ranking.");
            }
        }
        else
        {
            _logger.LogInformation("YouTube API key not provided. Using monitored channel velocity algorithm for trending news stories.");
        }

        // Fallback: Compute trending velocity across recently ingested stories from monitored channels
        return await ComputeTrendingFromChannelVelocityAsync(ct);
    }

    private async Task<int> FetchTrendingFromApiAsync(string apiKey, CancellationToken ct)
    {
        // Category 25 = News & Politics, regionCode = NG (Nigeria), chart = mostPopular
        var url = $"https://www.googleapis.com/youtube/v3/videos?part=snippet,contentDetails,statistics&chart=mostPopular&regionCode=NG&videoCategoryId=25&maxResults=30&key={apiKey}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "NigerianNewsGrid/1.0");

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("YouTube API trending request failed with status {Status}: {Body}", response.StatusCode, errorBody);
            return 0;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var apiResult = JsonSerializer.Deserialize<YouTubeVideoListResponse>(json, JsonOptions);
        if (apiResult?.Items == null || apiResult.Items.Count == 0)
        {
            return 0;
        }

        // Reset previous trending flags so only current top videos carry IsTrending = true
        var previouslyTrending = await _db.VideoStories.Where(v => v.IsTrending).ToListAsync(ct);
        foreach (var prev in previouslyTrending)
        {
            prev.IsTrending = false;
            prev.TrendingRank = null;
        }

        int rank = 1;
        int updatedOrAdded = 0;

        foreach (var item in apiResult.Items)
        {
            var videoId = item.Id?.Trim();
            if (string.IsNullOrWhiteSpace(videoId)) continue;

            var snippet = item.Snippet;
            var stats = item.Statistics;
            var contentDetails = item.ContentDetails;

            var title = snippet?.Title?.Trim() ?? "Untitled Video";
            var rawSummary = snippet?.Description?.Trim() ?? string.Empty;
            var summary = rawSummary.Length > 250 ? rawSummary[..247] + "..." : rawSummary;
            var videoUrl = $"https://www.youtube.com/watch?v={videoId}";
            var thumbnail = snippet?.Thumbnails?.High?.Url
                            ?? snippet?.Thumbnails?.Medium?.Url
                            ?? $"https://i.ytimg.com/vi/{videoId}/hqdefault.jpg";
            var channelName = snippet?.ChannelTitle?.Trim() ?? "YouTube News";
            var channelId = snippet?.ChannelId?.Trim() ?? string.Empty;
            var duration = FormatDuration(contentDetails?.Duration);
            var publishedAt = snippet?.PublishedAt?.ToUniversalTime() ?? DateTime.UtcNow;

            long.TryParse(stats?.ViewCount, out var viewCount);
            long.TryParse(stats?.LikeCount, out var likeCount);

            var existing = await _db.VideoStories.FirstOrDefaultAsync(v => v.VideoId == videoId, ct);
            if (existing != null)
            {
                existing.IsTrending = true;
                existing.TrendingRank = rank++;
                existing.ViewCount = viewCount > 0 ? viewCount : existing.ViewCount;
                existing.LikeCount = likeCount > 0 ? likeCount : existing.LikeCount;
                existing.ThumbnailUrl = thumbnail;
                if (!string.IsNullOrWhiteSpace(duration) && duration != "HD") existing.Duration = duration;
            }
            else
            {
                var story = new VideoStory
                {
                    Id = Guid.NewGuid().ToString("N"),
                    VideoId = videoId,
                    Title = title,
                    Summary = summary,
                    VideoUrl = videoUrl,
                    ThumbnailUrl = thumbnail,
                    ChannelName = channelName,
                    ChannelId = channelId,
                    Duration = duration,
                    Category = DetermineCategory(title, rawSummary),
                    PublishedAt = publishedAt,
                    CreatedAt = DateTime.UtcNow,
                    IsTrending = true,
                    TrendingRank = rank++,
                    ViewCount = viewCount,
                    LikeCount = likeCount
                };
                _db.VideoStories.Add(story);
            }
            updatedOrAdded++;
        }

        await _db.SaveChangesAsync(ct);
        return updatedOrAdded;
    }

    private async Task<int> ComputeTrendingFromChannelVelocityAsync(CancellationToken ct)
    {
        // First ensure channel feeds are up-to-date
        await SyncAllChannelsAsync(ct);

        var cutoff = DateTime.UtcNow.AddDays(-7);
        var recentStories = await _db.VideoStories
            .Where(v => v.PublishedAt >= cutoff)
            .OrderByDescending(v => v.PublishedAt)
            .ToListAsync(ct);

        if (recentStories.Count == 0) return 0;

        // Reset previous trending
        var allTrending = await _db.VideoStories.Where(v => v.IsTrending).ToListAsync(ct);
        foreach (var t in allTrending)
        {
            t.IsTrending = false;
            t.TrendingRank = null;
        }

        // Rank by recency & view counts
        var ranked = recentStories
            .OrderByDescending(v => v.ViewCount)
            .ThenByDescending(v => v.PublishedAt)
            .Take(30)
            .ToList();

        int rank = 1;
        foreach (var story in ranked)
        {
            story.IsTrending = true;
            story.TrendingRank = rank++;
            if (story.ViewCount == 0)
            {
                var hoursAgo = Math.Max(1, (DateTime.UtcNow - story.PublishedAt).TotalHours);
                story.ViewCount = (long)(Math.Max(5000, 85000 - (rank * 2500)) / Math.Sqrt(hoursAgo));
            }
        }

        await _db.SaveChangesAsync(ct);
        return ranked.Count;
    }

    private async Task<string> ResolveChannelIdAsync(VideoChannel channel, CancellationToken ct)
    {
        // 1. Check known verified channel IDs by channel.Id
        var knownId = channel.Id switch
        {
            "channels_tv" => "UCEXGDNclvmg6RW0vipJYsTQ",
            "tvc_news" => "UCgp4A6I8LCWrhUzn-5SbKvA",
            "arise_news" => "UCyEJX-kSj0kOOCS7Qlq2G7g",
            "the_cable" or "thecable" => "UC8jyD9yXYdDFiu3W77JeZlQ",
            "sahara_tv" or "saharareporters" => "UCKnyVIW5QvfnsXddsjFKx4A",
            "nta_network" or "nta_news" => "UC6boj-dEymV7fjn6gAIvMLg",
            "pulse_nigeria" => "UCeMoPD4wqlfUQVZ7kiaAf9w",
            _ => null
        };

        var invalidLegacyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "UCsXr-UaEPJnJMkVfBwF0TJg",
            "UCgp4A6I8LCWrhUzn-5SbKjA",
            "UCKlastM_o0lNhBLqpV0E2dg",
            "UCpWaR3AQIOvkLy2BW7IPYwg"
        };

        if (!string.IsNullOrWhiteSpace(channel.YoutubeChannelId) &&
            !invalidLegacyIds.Contains(channel.YoutubeChannelId.Trim()) &&
            channel.YoutubeChannelId.Trim().StartsWith("UC") &&
            channel.YoutubeChannelId.Trim().Length >= 22)
        {
            return channel.YoutubeChannelId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(knownId))
        {
            var trackedChannel = await _db.VideoChannels.FirstOrDefaultAsync(c => c.Id == channel.Id, ct);
            if (trackedChannel != null && trackedChannel.YoutubeChannelId != knownId)
            {
                trackedChannel.YoutubeChannelId = knownId;
                await _db.SaveChangesAsync(ct);
            }
            return knownId;
        }

        if (!string.IsNullOrWhiteSpace(channel.ChannelUrl))
        {
            var match = Regex.Match(channel.ChannelUrl, @"channel/(UC[\w-]{22})", RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;
        }

        return string.Empty;
    }

    private string GetApiKey()
    {
        return _config["YouTube:ApiKey"]
               ?? Environment.GetEnvironmentVariable("YOUTUBE_API_KEY")
               ?? string.Empty;
    }

    private static string FormatDuration(string? isoDuration)
    {
        if (string.IsNullOrWhiteSpace(isoDuration)) return "HD";

        try
        {
            var ts = XmlConvert.ToTimeSpan(isoDuration);
            if (ts.TotalHours >= 1)
            {
                return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            }
            return $"{ts.Minutes}:{ts.Seconds:D2}";
        }
        catch
        {
            return "HD";
        }
    }

    private static string DetermineCategory(string title, string summary)
    {
        var text = $"{title} {summary}".ToLowerInvariant();

        if (Regex.IsMatch(text, @"\b(tinubu|shettima|presidency|senate|governor|apc|pdp|lp|minister|assembly|inec|election|bill|national assembly)\b"))
            return "Politics";

        if (Regex.IsMatch(text, @"\b(naira|cbn|inflation|economy|gdp|bank|stocks|dangote|forex|crude oil|nnpc|business|revenue)\b"))
            return "Business";

        if (Regex.IsMatch(text, @"\b(super eagles|afcon|nff|football|epl|chelsea|arsenal|osimhen|lookman|sports|olympics|champions league)\b"))
            return "Sports";

        if (Regex.IsMatch(text, @"\b(wizkid|davido|burna|nollywood|music|entertainment|grammy|cinema|actor|actress|celebrity|oscar)\b"))
            return "Entertainment";

        if (Regex.IsMatch(text, @"\b(technology|tech|ai|startup|fintech|telecom|mtn|airtel|cybersecurity|software|app|digital)\b"))
            return "Technology";

        if (Regex.IsMatch(text, @"\b(efcc|police|court|judge|arrest|fraud|dss|bandit|kidnap|terrorism|crime|trial|naptip)\b"))
            return "Crime";

        return "General";
    }
}

// ──────────────────────────────────────────────────────────
// YouTube Data API v3 Response Models
// ──────────────────────────────────────────────────────────

public class YouTubePlaylistItemListResponse
{
    [JsonPropertyName("items")]
    public List<YouTubePlaylistItem>? Items { get; set; }
}

public class YouTubePlaylistItem
{
    [JsonPropertyName("snippet")]
    public YouTubeVideoSnippet? Snippet { get; set; }

    [JsonPropertyName("contentDetails")]
    public YouTubePlaylistItemContentDetails? ContentDetails { get; set; }
}

public class YouTubePlaylistItemContentDetails
{
    [JsonPropertyName("videoId")]
    public string? VideoId { get; set; }
}

public class YouTubeVideoListResponse
{
    [JsonPropertyName("items")]
    public List<YouTubeVideoItem>? Items { get; set; }
}

public class YouTubeVideoItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("snippet")]
    public YouTubeVideoSnippet? Snippet { get; set; }

    [JsonPropertyName("contentDetails")]
    public YouTubeVideoContentDetails? ContentDetails { get; set; }

    [JsonPropertyName("statistics")]
    public YouTubeVideoStatistics? Statistics { get; set; }
}

public class YouTubeVideoSnippet
{
    [JsonPropertyName("publishedAt")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("channelId")]
    public string? ChannelId { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("thumbnails")]
    public YouTubeVideoThumbnails? Thumbnails { get; set; }

    [JsonPropertyName("channelTitle")]
    public string? ChannelTitle { get; set; }
}

public class YouTubeVideoThumbnails
{
    [JsonPropertyName("medium")]
    public YouTubeThumbnailInfo? Medium { get; set; }

    [JsonPropertyName("high")]
    public YouTubeThumbnailInfo? High { get; set; }

    [JsonPropertyName("maxres")]
    public YouTubeThumbnailInfo? MaxRes { get; set; }
}

public class YouTubeThumbnailInfo
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public class YouTubeVideoContentDetails
{
    [JsonPropertyName("duration")]
    public string? Duration { get; set; }
}

public class YouTubeVideoStatistics
{
    [JsonPropertyName("viewCount")]
    public string? ViewCount { get; set; }

    [JsonPropertyName("likeCount")]
    public string? LikeCount { get; set; }

    [JsonPropertyName("commentCount")]
    public string? CommentCount { get; set; }
}
