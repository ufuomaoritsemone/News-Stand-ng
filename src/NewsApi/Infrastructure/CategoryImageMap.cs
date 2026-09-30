using System.Collections.Frozen;

namespace NewsApi.Infrastructure;

/// <summary>
/// Single source of truth for category → fallback image URL mapping.
/// Used by both ArticlesController and any client-side fallback logic.
/// Open/Closed compliant: extend the dictionary, never modify the caller.
/// </summary>
public static class CategoryImageMap
{
    private static readonly FrozenDictionary<string, string> _map =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["politics"]      = "https://images.unsplash.com/photo-1540910419892-4a36d2c3266c?w=600&auto=format&fit=crop&q=80",
            ["government"]    = "https://images.unsplash.com/photo-1540910419892-4a36d2c3266c?w=600&auto=format&fit=crop&q=80",
            ["business"]      = "https://images.unsplash.com/photo-1611974789855-9c2a0a7236a3?w=600&auto=format&fit=crop&q=80",
            ["economy"]       = "https://images.unsplash.com/photo-1611974789855-9c2a0a7236a3?w=600&auto=format&fit=crop&q=80",
            ["finance"]       = "https://images.unsplash.com/photo-1611974789855-9c2a0a7236a3?w=600&auto=format&fit=crop&q=80",
            ["sports"]        = "https://images.unsplash.com/photo-1508098682722-e99c43a406b2?w=600&auto=format&fit=crop&q=80",
            ["technology"]    = "https://images.unsplash.com/photo-1518770660439-4636190af475?w=600&auto=format&fit=crop&q=80",
            ["tech"]          = "https://images.unsplash.com/photo-1518770660439-4636190af475?w=600&auto=format&fit=crop&q=80",
            ["entertainment"] = "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=600&auto=format&fit=crop&q=80",
            ["health"]        = "https://images.unsplash.com/photo-1505751172876-fa1923c5c528?w=600&auto=format&fit=crop&q=80",
            ["education"]     = "https://images.unsplash.com/photo-1503676260728-1c00da094a0b?w=600&auto=format&fit=crop&q=80",
            ["international"] = "https://images.unsplash.com/photo-1526778548025-fa2f459cd5c1?w=600&auto=format&fit=crop&q=80",
            ["world"]         = "https://images.unsplash.com/photo-1526778548025-fa2f459cd5c1?w=600&auto=format&fit=crop&q=80",
            ["foreign"]       = "https://images.unsplash.com/photo-1526778548025-fa2f459cd5c1?w=600&auto=format&fit=crop&q=80",
            ["global"]        = "https://images.unsplash.com/photo-1526778548025-fa2f459cd5c1?w=600&auto=format&fit=crop&q=80",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private const string DefaultFallback =
        "https://images.unsplash.com/photo-1585829365295-ab7cd400c167?w=600&auto=format&fit=crop&q=80";

    /// <summary>
    /// Returns the best available image URL: the existing URL if valid,
    /// a category-matched fallback, or the global default.
    /// </summary>
    public static string Resolve(string? existingUrl, string? category)
    {
        if (!string.IsNullOrWhiteSpace(existingUrl) &&
            (existingUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
             existingUrl.StartsWith("http://",  StringComparison.OrdinalIgnoreCase)))
        {
            return existingUrl;
        }

        if (string.IsNullOrWhiteSpace(category))
            return DefaultFallback;

        // Try exact match first, then prefix match for compound categories ("Business & Economy")
        if (_map.TryGetValue(category.Trim(), out var exact))
            return exact;

        foreach (var (key, url) in _map)
        {
            if (category.Contains(key, StringComparison.OrdinalIgnoreCase))
                return url;
        }

        return DefaultFallback;
    }
}
