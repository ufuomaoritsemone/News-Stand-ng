using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace NewsScraperService.Services;

/// <summary>
/// Manages the crawl frontier and deduplication of seen URLs and article fingerprints.
/// Prevents redundant HTTP fetches across sitemaps, RSS feeds, and crawling cycles.
/// </summary>
public class UrlFrontierManager
{
    private readonly ConcurrentDictionary<string, DateTime> _seenUrls = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _seenTitleFingerprints = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _retentionPeriod;
    private readonly int _maxCapacity;

    public UrlFrontierManager(TimeSpan? retentionPeriod = null, int maxCapacity = 10000)
    {
        _retentionPeriod = retentionPeriod ?? TimeSpan.FromDays(7);
        _maxCapacity = maxCapacity;
    }

    /// <summary>
    /// Normalizes a URL by stripping tracking parameters, session IDs, and fragments.
    /// </summary>
    public static string NormalizeUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return string.Empty;

        try
        {
            if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri))
            {
                return rawUrl.Trim();
            }

            var builder = new UriBuilder(uri)
            {
                Fragment = string.Empty // Remove #anchor
            };

            // Parse and filter query parameters
            var query = uri.Query.TrimStart('?');
            if (!string.IsNullOrEmpty(query))
            {
                var cleanParams = new List<string>();
                var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries);

                foreach (var pair in pairs)
                {
                    var parts = pair.Split('=', 2);
                    var key = parts[0].ToLowerInvariant();

                    // Strip tracking query parameters
                    if (key.StartsWith("utm_") || 
                        key == "fbclid" || 
                        key == "gclid" || 
                        key == "ref" || 
                        key == "ref_src" || 
                        key == "source" || 
                        key == "amp" ||
                        key == "_hsenc" ||
                        key == "_hsmi")
                    {
                        continue;
                    }

                    cleanParams.Add(pair);
                }

                builder.Query = cleanParams.Count > 0 ? string.Join("&", cleanParams) : string.Empty;
            }

            var normalized = builder.Uri.ToString();
            // Remove trailing slash if path is more than just "/"
            if (normalized.EndsWith("/") && builder.Path.Length > 1)
            {
                normalized = normalized.TrimEnd('/');
            }

            return normalized;
        }
        catch
        {
            return rawUrl.Trim();
        }
    }

    /// <summary>
    /// Generates a normalized alphanumeric title fingerprint for duplicate wire story detection.
    /// </summary>
    public static string GenerateTitleFingerprint(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        // Remove punctuation, extra spaces, and lowercase
        var clean = Regex.Replace(title.ToLowerInvariant(), @"[^\w\s]", " ");
        clean = Regex.Replace(clean, @"\s+", " ").Trim();
        return clean;
    }

    /// <summary>
    /// Checks if a URL has already been seen in recent cycles.
    /// </summary>
    public bool IsUrlSeen(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        var normalized = NormalizeUrl(url);
        if (_seenUrls.TryGetValue(normalized, out var seenTime))
        {
            if (DateTime.UtcNow - seenTime < _retentionPeriod)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Marks a URL as seen. Returns true if URL is new, false if already present.
    /// </summary>
    public bool MarkUrlSeen(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var normalized = NormalizeUrl(url);

        PruneIfNecessary();

        var isNew = !_seenUrls.ContainsKey(normalized);
        _seenUrls[normalized] = DateTime.UtcNow;
        return isNew;
    }

    /// <summary>
    /// Marks an article title fingerprint as seen. Returns true if unique, false if seen.
    /// </summary>
    public bool MarkTitleSeen(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var fingerprint = GenerateTitleFingerprint(title);
        if (string.IsNullOrEmpty(fingerprint)) return false;

        PruneIfNecessary();

        var isNew = !_seenTitleFingerprints.ContainsKey(fingerprint);
        _seenTitleFingerprints[fingerprint] = DateTime.UtcNow;
        return isNew;
    }

    private void PruneIfNecessary()
    {
        if (_seenUrls.Count > _maxCapacity)
        {
            var cutoff = DateTime.UtcNow - _retentionPeriod;
            foreach (var kvp in _seenUrls)
            {
                if (kvp.Value < cutoff)
                {
                    _seenUrls.TryRemove(kvp.Key, out _);
                }
            }
        }

        if (_seenTitleFingerprints.Count > _maxCapacity)
        {
            var cutoff = DateTime.UtcNow - _retentionPeriod;
            foreach (var kvp in _seenTitleFingerprints)
            {
                if (kvp.Value < cutoff)
                {
                    _seenTitleFingerprints.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}
