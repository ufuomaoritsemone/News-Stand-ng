using System.Text.Json;
using Microsoft.Extensions.Logging;
using NigerianNewGrid.Constants;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewGrid.Services;

/// <summary>
/// Manages local briefing cache using Preferences storage.
/// Fix #37: Uses ILogger instead of Debug.WriteLine.
/// Fix #24: Uses AppPreferenceKeys constant instead of magic string.
/// </summary>
public class BriefingCacheService(ILogger<BriefingCacheService> logger) : IBriefingCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public Task<List<BriefingCategory>> GetCachedBriefingAsync()
    {
        try
        {
            var cachedJson = Preferences.Get(AppPreferenceKeys.LastBriefing, string.Empty);
            if (!string.IsNullOrWhiteSpace(cachedJson))
            {
                var categories = JsonSerializer.Deserialize<List<BriefingCategory>>(cachedJson, JsonOptions);
                if (categories is { Count: > 0 })
                    return Task.FromResult(categories);
            }
        }
        catch (Exception ex)
        {
            // Fix #37 — structured log via ILogger instead of Debug.WriteLine
            logger.LogWarning(ex, "Cache deserialization error reading briefing.");
        }

        return Task.FromResult(new List<BriefingCategory>());
    }

    public Task SaveBriefingAsync(List<BriefingCategory> categories)
    {
        try
        {
            if (categories is { Count: > 0 })
            {
                var json = JsonSerializer.Serialize(categories, JsonOptions);
                Preferences.Set(AppPreferenceKeys.LastBriefing, json);
            }
        }
        catch (Exception ex)
        {
            // Fix #37 — structured log via ILogger
            logger.LogWarning(ex, "Cache write error saving briefing.");
        }

        return Task.CompletedTask;
    }

    public List<BriefingCategory> GetFallbackSampleBriefing(string language)
    {
        BriefingItem MakeItem(string title, string summary, string url, string source, string category) =>
            new()
            {
                Id          = Guid.NewGuid().ToString(),
                Title       = title,
                Summary     = summary,
                Url         = url,
                Source      = source,
                Category    = category,
                ImageUrl    = ResolveCategory(category),
                PublishedAt = DateTime.UtcNow.AddHours(-2),
                VoiceName   = $"{language} Female"
            };

        return
        [
            new BriefingCategory
            {
                Category = "Politics",
                Top =
                [
                    MakeItem("Federal Government Unveils New Digital Economy Roadmap",
                        "The Ministry of Communications and Digital Economy announced a strategic initiative targeting infrastructure expansion, broadband coverage, and youth technical skill development nationwide.",
                        "https://punchng.com/news/digital-roadmap", "Punch Newspaper", "Politics"),
                    MakeItem("National Assembly Passes Key Energy & Power Sector Reform Bill",
                        "Lawmakers approved comprehensive legislative measures to enhance power grid reliability and boost renewable energy investments across state governments.",
                        "https://guardian.ng/news/energy-bill-passed", "The Guardian Nigeria", "Politics")
                ]
            },
            new BriefingCategory
            {
                Category = "Sports",
                Top =
                [
                    MakeItem("Super Eagles Prepare for International Friendly Match",
                        "Coaching staff confirmed full squad arrival at camp ahead of weekend international clash, highlighting tactical adjustments and player fitness.",
                        "https://guardian.ng/sports/super-eagles-friendly", "The Guardian Nigeria", "Sports")
                ]
            },
            new BriefingCategory
            {
                Category = "Business",
                Top =
                [
                    MakeItem("Central Bank Highlights Monetary Policy & Foreign Exchange Outlook",
                        "Key financial indicators show steady stabilization across foreign exchange markets, trade balances, and agricultural sector loans.",
                        "https://www.premiumtimesng.com/business/cbn-monetary-policy", "Premium Times", "Business")
                ]
            },
            new BriefingCategory
            {
                Category = "Technology",
                Top =
                [
                    MakeItem("Tech Hub Ecosystem Expands Across Lagos, Abuja and Port Harcourt",
                        "Venture capital investments in Nigerian fintech and artificial intelligence startups reached new record milestones this quarter.",
                        "https://punchng.com/tech/startup-growth", "Punch Newspaper", "Technology")
                ]
            }
        ];
    }

    // Fix #24 — replaces local hardcoded if-chain with a shared lookup
    private static readonly Dictionary<string, string> FallbackImages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Politics"]      = "https://images.unsplash.com/photo-1540910419892-4a36d2c3266c?w=600&auto=format&fit=crop&q=80",
        ["Business"]      = "https://images.unsplash.com/photo-1611974789855-9c2a0a7236a3?w=600&auto=format&fit=crop&q=80",
        ["Sports"]        = "https://images.unsplash.com/photo-1508098682722-e99c43a406b2?w=600&auto=format&fit=crop&q=80",
        ["Technology"]    = "https://images.unsplash.com/photo-1518770660439-4636190af475?w=600&auto=format&fit=crop&q=80",
        ["Entertainment"] = "https://images.unsplash.com/photo-1514525253161-7a46d19cd819?w=600&auto=format&fit=crop&q=80",
    };

    private static string ResolveCategory(string category) =>
        FallbackImages.GetValueOrDefault(category,
            "https://images.unsplash.com/photo-1585829365295-ab7cd400c167?w=600&auto=format&fit=crop&q=80");
}
