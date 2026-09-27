using System;
using System.Collections.Generic;
using System.Linq;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewsGrid.Client.Helpers;

/// <summary>
/// Architecture Component: Feed Slot Placement & Conflict-Avoidance Engine.
/// 
/// Deterministically positions Sponsored Stories and Native AdMob ad cards in the user feed:
/// - Special Placement Slots: 1st, 2nd, 3rd position (Premium / Hero sponsor tier)
/// - Position 4: Preserved as organic editorial buffer
/// - Standard In-Feed Slots: 5th through 30th position
/// - Conflict Resolution: Priority weight and freshness tie-breaking with cascading slot bumping
/// - Ad Density & Separation: Enforces minimum organic story separation between in-feed ads
/// - Programmatic Backfill: Injects native AdMob ad cards into vacant benchmark slots
/// </summary>
public static class FeedSlotPlacementHelper
{
    public const int MinStandardPosition = 5;
    public const int MaxStandardPosition = 30;
    public const int DefaultMinOrganicSeparation = 3;

    /// <summary>
    /// Arranges organic stories, active sponsored stories, and programmatic Native AdMob cards
    /// into a unified, conflict-free, and density-regulated news feed.
    /// </summary>
    /// <param name="organicStories">The deduplicated organic news articles.</param>
    /// <param name="sponsoredStories">Active sponsored campaigns with target placement requests.</param>
    /// <param name="includeAdMobPlaceholders">Whether to backfill unoccupied benchmark slots with Native AdMob ads.</param>
    /// <param name="adMobBenchmarkSlots">Benchmark slots for Native AdMob ads (default: 10, 20).</param>
    /// <param name="minSeparation">Minimum number of organic stories between two ads in the in-feed zone.</param>
    /// <returns>A unified list of BriefingItem representing the ordered feed.</returns>
    public static List<BriefingItem> ArrangeFeed(
        IReadOnlyList<BriefingItem> organicStories,
        IReadOnlyList<BriefingItem> sponsoredStories,
        bool includeAdMobPlaceholders = true,
        IReadOnlyList<int>? adMobBenchmarkSlots = null,
        int minSeparation = DefaultMinOrganicSeparation)
    {
        ArgumentNullException.ThrowIfNull(organicStories);
        ArgumentNullException.ThrowIfNull(sponsoredStories);

        adMobBenchmarkSlots ??= [10, 20];

        // 1. Separate organic stories (ensure no sponsored or ad placeholders are mixed in)
        var pureOrganic = organicStories
            .Where(s => !s.IsSponsored && !s.IsAdMobPlaceholder)
            .ToList();

        // 2. Resolve conflicting sponsored story placements into concrete 1-based slots
        var placedItems = ResolveSponsoredPlacements(sponsoredStories, minSeparation);

        // 3. Inject programmatic Native AdMob cards into benchmark slots if vacant and compliant with separation
        if (includeAdMobPlaceholders)
        {
            InjectAdMobPlaceholders(placedItems, adMobBenchmarkSlots, minSeparation);
        }

        // 4. Merge placed items into the organic stream
        return MergeIntoFeed(pureOrganic, placedItems);
    }

    /// <summary>
    /// Validates whether a target position is a valid special slot (1, 2, 3) or standard slot (5..30).
    /// </summary>
    public static bool IsValidTargetPosition(int position)
    {
        return (position >= 1 && position <= 3) || (position >= MinStandardPosition && position <= MaxStandardPosition);
    }

    /// <summary>
    /// Resolves target slot conflicts across sponsored stories using priority weight, publication date,
    /// and cascading bumping.
    /// </summary>
    private static Dictionary<int, BriefingItem> ResolveSponsoredPlacements(
        IReadOnlyList<BriefingItem> sponsoredStories,
        int minSeparation)
    {
        var slotMap = new Dictionary<int, BriefingItem>();

        // Sort candidates: pinned first, then higher priority weight, then newest published
        var candidates = sponsoredStories
            .Where(s => s.IsSponsored)
            .OrderByDescending(s => s.IsPinned)
            .ThenByDescending(s => s.PriorityWeight)
            .ThenByDescending(s => s.PublishedAt ?? DateTime.MinValue)
            .ToList();

        foreach (var candidate in candidates)
        {
            var requestedSlot = candidate.TargetPosition ?? (candidate.IsPinned ? 1 : MinStandardPosition);

            // Normalize out-of-bounds requested positions
            if (!IsValidTargetPosition(requestedSlot))
            {
                requestedSlot = requestedSlot < MinStandardPosition ? 1 : MaxStandardPosition;
            }

            var assignedSlot = FindAvailableSlot(slotMap, requestedSlot, minSeparation);
            if (assignedSlot.HasValue)
            {
                slotMap[assignedSlot.Value] = candidate with { TargetPosition = assignedSlot.Value };
            }
        }

        return slotMap;
    }

    /// <summary>
    /// Finds the closest available slot for a candidate, observing slot validity and separation rules.
    /// </summary>
    private static int? FindAvailableSlot(
        Dictionary<int, BriefingItem> slotMap,
        int requestedSlot,
        int minSeparation)
    {
        // Special Placements: 1, 2, 3
        if (requestedSlot <= 3)
        {
            for (var slot = requestedSlot; slot <= 3; slot++)
            {
                if (!slotMap.ContainsKey(slot))
                {
                    return slot;
                }
            }

            // If all special slots (1, 2, 3) are full, bump into standard in-feed zone starting at position 5
            requestedSlot = MinStandardPosition;
        }

        // Standard In-Feed Placements: 5..30
        for (var slot = requestedSlot; slot <= MaxStandardPosition; slot++)
        {
            if (slotMap.ContainsKey(slot)) continue;

            // Enforce minimum separation distance in standard in-feed zone
            if (IsSeparationRespected(slotMap.Keys, slot, minSeparation))
            {
                return slot;
            }
        }

        // If no slot <= 30 satisfies separation, try any unoccupied slot <= 30 without separation
        for (var slot = MinStandardPosition; slot <= MaxStandardPosition; slot++)
        {
            if (!slotMap.ContainsKey(slot))
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a candidate slot maintains minimum distance from all existing standard in-feed ads.
    /// Special slots (1, 2, 3) are excluded from the in-feed spacing rule.
    /// </summary>
    private static bool IsSeparationRespected(IEnumerable<int> occupiedSlots, int candidateSlot, int minSeparation)
    {
        if (candidateSlot <= 3) return true;

        foreach (var occupied in occupiedSlots)
        {
            if (occupied <= 3) continue; // Skip special tier
            if (Math.Abs(occupied - candidateSlot) <= minSeparation)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Injects programmatic Native AdMob placeholders into benchmark slots if open and compliant with separation.
    /// </summary>
    private static void InjectAdMobPlaceholders(
        Dictionary<int, BriefingItem> slotMap,
        IReadOnlyList<int> benchmarkSlots,
        int minSeparation)
    {
        foreach (var benchSlot in benchmarkSlots)
        {
            if (benchSlot < MinStandardPosition || benchSlot > MaxStandardPosition) continue;
            if (slotMap.ContainsKey(benchSlot)) continue;

            if (IsSeparationRespected(slotMap.Keys, benchSlot, minSeparation))
            {
                slotMap[benchSlot] = CreateNativeAdPlaceholder(benchSlot);
            }
        }
    }

    /// <summary>
    /// Factory for creating an in-feed Native AdMob placeholder item.
    /// </summary>
    public static BriefingItem CreateNativeAdPlaceholder(int position)
    {
        return new BriefingItem
        {
            Id                 = $"admob-native-{position}-{Guid.NewGuid():N}",
            Title              = "Sponsored Partner Promotion",
            Summary            = "Relevant partner story and native promotion.",
            Source             = "Google AdMob",
            Category           = "Promotion",
            PublishedAt        = DateTime.UtcNow,
            IsSponsored        = true,
            IsAdMobPlaceholder = true,
            TargetPosition     = position,
            SponsorName        = "Featured Sponsor"
        };
    }

    /// <summary>
    /// Merges placed ads (1-indexed slots) into the stream of organic articles.
    /// </summary>
    private static List<BriefingItem> MergeIntoFeed(
        List<BriefingItem> pureOrganic,
        Dictionary<int, BriefingItem> placedItems)
    {
        var result = new List<BriefingItem>(pureOrganic.Count + placedItems.Count);
        var organicIndex = 0;
        var currentSlot = 1;

        var totalExpectedItems = pureOrganic.Count + placedItems.Count;

        while (result.Count < totalExpectedItems)
        {
            if (placedItems.TryGetValue(currentSlot, out var adItem))
            {
                result.Add(adItem);
            }
            else if (organicIndex < pureOrganic.Count)
            {
                result.Add(pureOrganic[organicIndex]);
                organicIndex++;
            }
            else
            {
                // In case organic articles ran out before reaching higher ad slots
                break;
            }

            currentSlot++;
        }

        // Append any remaining organic articles
        while (organicIndex < pureOrganic.Count)
        {
            result.Add(pureOrganic[organicIndex]);
            organicIndex++;
        }

        // Append any remaining placed items that could not be slotted because organic stories were too few
        foreach (var kvp in placedItems.OrderBy(k => k.Key))
        {
            if (!result.Contains(kvp.Value))
            {
                result.Add(kvp.Value);
            }
        }

        return result;
    }
}
