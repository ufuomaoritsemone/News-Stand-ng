using System;
using System.Collections.Generic;
using System.Linq;
using NigerianNewsGrid.Client.Models;

namespace NigerianNewsGrid.Client.Helpers;

/// <summary>
/// Architecture Component: Feed Queue & Sliding-Window Eviction Engine.
/// 
/// Enforces a strict capacity cap (default: 60 feeds) on the main news screen:
/// - Arranges feeds like a FIFO queue where incoming new stories are enqueued at the top
///   and the equivalent count of oldest organic stories are dequeued from the tail.
/// - Sponsored feeds hold their fixed anchor positions (Special Slots 1-3, In-Feed Slots);
///   news feeds are arranged around them and never evict sponsored campaigns.
/// - 4 Native AdMob cards are maintained at benchmark slots spaced by 10 news feeds apart.
/// </summary>
public static class FeedQueueHelper
{
    public const int DefaultMaxFeedCapacity = 60;
    public static readonly IReadOnlyList<int> DefaultAdMobSlots = [11, 22, 33, 44];

    /// <summary>
    /// Enqueues new incoming stories into the feed using a FIFO sliding window capped at <paramref name="maxCapacity"/>.
    /// New feeds are enqueued at the front; the oldest organic feeds are dequeued from the back.
    /// Sponsored feeds hold their positions and news feeds are arranged around them.
    /// </summary>
    public static List<BriefingItem> EnqueueWithEviction(
        IReadOnlyList<BriefingItem> currentFeed,
        IReadOnlyList<BriefingItem> incomingStories,
        bool includeAdMobPlaceholders = true,
        int maxCapacity = DefaultMaxFeedCapacity,
        IReadOnlyList<int>? adMobBenchmarkSlots = null,
        int minSeparation = FeedSlotPlacementHelper.DefaultMinOrganicSeparation)
    {
        ArgumentNullException.ThrowIfNull(currentFeed);
        ArgumentNullException.ThrowIfNull(incomingStories);

        adMobBenchmarkSlots ??= DefaultAdMobSlots;

        // 1. Separate Sponsored Stories (Existing + Incoming) — Sponsored feeds hold their fixed positions
        var sponsoredMap = new Dictionary<string, BriefingItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var story in currentFeed.Where(s => s.IsSponsored && !s.IsAdMobPlaceholder))
        {
            if (!string.IsNullOrWhiteSpace(story.Id))
                sponsoredMap[story.Id] = story;
        }

        foreach (var story in incomingStories.Where(s => s.IsSponsored && !s.IsAdMobPlaceholder))
        {
            if (!string.IsNullOrWhiteSpace(story.Id))
                sponsoredMap[story.Id] = story;
        }

        var sponsoredStories = sponsoredMap.Values.ToList();

        // 2. Separate Organic Stories (Existing + Incoming)
        var existingOrganic = currentFeed
            .Where(s => !s.IsSponsored && !s.IsAdMobPlaceholder)
            .ToList();

        var existingIds = new HashSet<string>(
            existingOrganic.Select(s => s.Id).Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.OrdinalIgnoreCase);

        var existingSignatures = new HashSet<string>(
            existingOrganic.Select(GetSignature).Where(sig => !string.IsNullOrEmpty(sig)),
            StringComparer.OrdinalIgnoreCase);

        // Deduplicate incoming organic stories among themselves first
        var incomingOrganic = incomingStories
            .Where(s => !s.IsSponsored && !s.IsAdMobPlaceholder)
            .OrderByDescending(s => s.PublishedAt ?? DateTime.MinValue)
            .ToList();

        var uniqueIncomingOrganic = StoryDeduplicationHelper.DeduplicateStories(
            incomingOrganic,
            s => s.Title,
            s => s.Summary,
            s => s.Source)
            .ToList();

        // Identify genuinely new organic stories (not in existing feed)
        var brandNewOrganic = new List<BriefingItem>();
        var seenIncomingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var story in uniqueIncomingOrganic)
        {
            var sig = GetSignature(story);
            var isDuplicate = (!string.IsNullOrEmpty(story.Id) && existingIds.Contains(story.Id)) ||
                              (!string.IsNullOrEmpty(sig) && existingSignatures.Contains(sig)) ||
                              (!string.IsNullOrEmpty(story.Id) && !seenIncomingKeys.Add(story.Id)) ||
                              (!string.IsNullOrEmpty(sig) && !seenIncomingKeys.Add(sig));

            if (!isDuplicate)
            {
                brandNewOrganic.Add(story);
            }
        }

        // Sort brand-new organic stories descending by publication date
        brandNewOrganic = brandNewOrganic
            .OrderByDescending(s => s.PublishedAt ?? DateTime.MinValue)
            .ToList();

        // 3. Assemble Organic Queue: Newest incoming at the front, followed by existing organic stories
        var combinedOrganic = new List<BriefingItem>(brandNewOrganic.Count + existingOrganic.Count);
        combinedOrganic.AddRange(brandNewOrganic);
        combinedOrganic.AddRange(existingOrganic);

        // Re-deduplicate combined organic pool to guarantee uniqueness
        var deduplicatedOrganic = StoryDeduplicationHelper.DeduplicateStories(
            combinedOrganic,
            s => s.Title,
            s => s.Summary,
            s => s.Source)
            .OrderByDescending(s => s.PublishedAt ?? DateTime.MinValue)
            .ToList();

        // 4. Calculate bounded capacity and dequeue oldest organic stories from the tail
        // Total slots = maxCapacity (60)
        // Sponsored stories occupy their slots; native ads occupy benchmark slots
        int expectedAdCount = includeAdMobPlaceholders ? adMobBenchmarkSlots.Count : 0;
        int maxOrganicCapacity = Math.Max(0, maxCapacity - sponsoredStories.Count - expectedAdCount);

        // Take the newest organic items up to capacity (FIFO dequeue of the oldest items)
        var boundedOrganic = deduplicatedOrganic.Take(maxOrganicCapacity).ToList();

        // 5. Deterministically arrange the feed:
        // Sponsored feeds hold their positions, Native Ads spaced 10 news feeds apart, news feeds arranged around them
        var arrangedFeed = FeedSlotPlacementHelper.ArrangeFeed(
            boundedOrganic,
            sponsoredStories,
            includeAdMobPlaceholders: includeAdMobPlaceholders,
            adMobBenchmarkSlots: adMobBenchmarkSlots,
            minSeparation: minSeparation);

        // Enforce the strict overall feed capacity cap of 60
        return arrangedFeed.Take(maxCapacity).ToList();
    }

    /// <summary>
    /// Counts how many incoming stories are genuinely new compared to the active feed.
    /// When <paramref name="onlyNewerThanFeed"/> is true, only candidate stories published after
    /// the newest organic story in <paramref name="currentFeed"/> are counted, preventing older stories
    /// evicted from the tail of the bounded feed from triggering phantom notifications.
    /// </summary>
    public static int CountNewStories(
        IReadOnlyList<BriefingItem> currentFeed,
        IReadOnlyList<BriefingItem> candidateStories,
        bool onlyNewerThanFeed = false)
    {
        ArgumentNullException.ThrowIfNull(currentFeed);
        ArgumentNullException.ThrowIfNull(candidateStories);

        var existingIds = new HashSet<string>(
            currentFeed.Select(s => s.Id).Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.OrdinalIgnoreCase);

        var existingSignatures = new HashSet<string>(
            currentFeed.Select(GetSignature).Where(sig => !string.IsNullOrEmpty(sig)),
            StringComparer.OrdinalIgnoreCase);

        DateTime? latestFeedUtc = null;
        if (onlyNewerThanFeed && currentFeed.Count > 0)
        {
            var maxUtc = currentFeed
                .Where(s => !s.IsSponsored && !s.IsAdMobPlaceholder && s.PublishedAt.HasValue)
                .Select(s => s.PublishedAt!.Value)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            if (maxUtc > DateTime.MinValue)
            {
                latestFeedUtc = maxUtc;
            }
        }

        var count = 0;
        var countedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidateStories)
        {
            if (candidate.IsAdMobPlaceholder) continue;

            // When onlyNewerThanFeed is requested, ignore candidates that are older than or equal to the latest feed story
            if (latestFeedUtc.HasValue && (!candidate.PublishedAt.HasValue || candidate.PublishedAt.Value <= latestFeedUtc.Value))
            {
                continue;
            }

            var sig = GetSignature(candidate);
            var isKnown = (!string.IsNullOrEmpty(candidate.Id) && existingIds.Contains(candidate.Id)) ||
                          (!string.IsNullOrEmpty(sig) && existingSignatures.Contains(sig));

            if (!isKnown)
            {
                var key = !string.IsNullOrEmpty(candidate.Id) ? candidate.Id : sig;
                if (!string.IsNullOrEmpty(key) && countedKeys.Add(key))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static string GetSignature(BriefingItem item)
    {
        var title = (item.Title ?? string.Empty).Trim().ToLowerInvariant();
        var source = (item.Source ?? string.Empty).Trim().ToLowerInvariant();
        return $"{source}::{title}";
    }
}
