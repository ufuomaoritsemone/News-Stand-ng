using System;
using System.Collections.Generic;
using System.Linq;
using NigerianNewsGrid.Client.Helpers;
using NigerianNewsGrid.Client.Models;
using Xunit;

namespace NewsApiClient.Tests;

public class FeedQueueTests
{
    private static List<BriefingItem> GenerateOrganicStories(int count, int startOffsetMinutes = 0)
    {
        var list = new List<BriefingItem>(count);
        var baseTime = DateTime.UtcNow;
        for (var i = 1; i <= count; i++)
        {
            list.Add(new BriefingItem
            {
                Id = $"story-{startOffsetMinutes + i}",
                Title = $"Headline-{startOffsetMinutes + i}-{Guid.NewGuid():N}",
                Summary = $"Summary-{startOffsetMinutes + i}-{Guid.NewGuid():N}",
                Source = "National Daily",
                Category = "General",
                PublishedAt = baseTime.AddMinutes(-(startOffsetMinutes + i)),
                IsSponsored = false,
                IsAdMobPlaceholder = false
            });
        }
        return list;
    }

    [Fact]
    public void FeedQueue_UnderCapacity_RetainsAllStories()
    {
        // Arrange
        var existing = GenerateOrganicStories(20);
        var incoming = GenerateOrganicStories(10, startOffsetMinutes: 100);

        // Act
        var result = FeedQueueHelper.EnqueueWithEviction(
            existing,
            incoming,
            includeAdMobPlaceholders: false,
            maxCapacity: 60);

        // Assert
        Assert.Equal(30, result.Count);
    }

    [Fact]
    public void FeedQueue_CapsAtSixty_AndDequeuesOldestOrganicStories()
    {
        // Arrange: Start with 55 stories
        var existing = GenerateOrganicStories(55, startOffsetMinutes: 100);
        
        // Incoming: 10 brand-new stories (published more recently, e.g. offset 1..10)
        var incomingNew = GenerateOrganicStories(10, startOffsetMinutes: 0);

        // Act: Enqueue without ads to test pure story capacity
        var result = FeedQueueHelper.EnqueueWithEviction(
            existing,
            incomingNew,
            includeAdMobPlaceholders: false,
            maxCapacity: 60);

        // Assert
        Assert.Equal(60, result.Count);

        // Verify the 10 newest incoming stories are at the front of the queue
        for (var i = 1; i <= 10; i++)
        {
            Assert.Contains(result, s => s.Id == $"story-{i}");
        }

        // Verify the oldest stories from existing (e.g. story-151 to story-155) were dequeued
        Assert.DoesNotContain(result, s => s.Id == "story-155");
        Assert.DoesNotContain(result, s => s.Id == "story-154");
    }

    [Fact]
    public void NativeAds_ExactlyFourPlaced_SpacedByTenNewsFeeds()
    {
        // Arrange: 60 organic stories
        var organic = GenerateOrganicStories(60);

        // Act: Enqueue with AdMob placeholders enabled
        var feed = FeedQueueHelper.EnqueueWithEviction(
            currentFeed: [],
            incomingStories: organic,
            includeAdMobPlaceholders: true,
            maxCapacity: 60);

        // Assert: Total feeds capped at 60
        Assert.Equal(60, feed.Count);

        // Native ads count must be exactly 4
        var nativeAds = feed.Where(s => s.IsAdMobPlaceholder).ToList();
        Assert.Equal(4, nativeAds.Count);

        // Check benchmark slots [11, 22, 33, 44] (indices 10, 21, 32, 43)
        Assert.True(feed[10].IsAdMobPlaceholder, "Expected Native Ad 1 at slot 11 (index 10)");
        Assert.True(feed[21].IsAdMobPlaceholder, "Expected Native Ad 2 at slot 22 (index 21)");
        Assert.True(feed[32].IsAdMobPlaceholder, "Expected Native Ad 3 at slot 33 (index 32)");
        Assert.True(feed[43].IsAdMobPlaceholder, "Expected Native Ad 4 at slot 44 (index 43)");

        // Verify that between slot 11 and slot 22 there are exactly 10 news feeds (indices 11..20)
        for (int i = 11; i <= 20; i++)
        {
            Assert.False(feed[i].IsAdMobPlaceholder, $"Expected organic news feed at index {i} between ads");
        }

        // Verify that between slot 22 and slot 33 there are exactly 10 news feeds (indices 22..31)
        for (int i = 22; i <= 31; i++)
        {
            Assert.False(feed[i].IsAdMobPlaceholder, $"Expected organic news feed at index {i} between ads");
        }

        // Verify that between slot 33 and slot 44 there are exactly 10 news feeds (indices 33..42)
        for (int i = 33; i <= 42; i++)
        {
            Assert.False(feed[i].IsAdMobPlaceholder, $"Expected organic news feed at index {i} between ads");
        }
    }

    [Fact]
    public void SponsoredFeeds_HoldPositions_NewsFeedsArrangedAroundThem()
    {
        // Arrange: Existing feed with 2 sponsored posts: Slot 1 (Hero) and Slot 5 (In-feed)
        var sponsored1 = new BriefingItem
        {
            Id = "sponsor-hero",
            Title = "Hero Sponsor Brand",
            IsSponsored = true,
            TargetPosition = 1,
            IsPinned = true
        };
        var sponsored5 = new BriefingItem
        {
            Id = "sponsor-slot-5",
            Title = "Fintech Sponsor",
            IsSponsored = true,
            TargetPosition = 5
        };

        var initialOrganic = GenerateOrganicStories(40, startOffsetMinutes: 100);
        var initialFeed = FeedQueueHelper.EnqueueWithEviction(
            currentFeed: [],
            incomingStories: [sponsored1, sponsored5, ..initialOrganic],
            includeAdMobPlaceholders: false,
            maxCapacity: 60);

        Assert.Equal("sponsor-hero", initialFeed[0].Id);   // Slot 1
        Assert.Equal("sponsor-slot-5", initialFeed[4].Id); // Slot 5

        // Incoming: 10 brand-new organic stories
        var freshStories = GenerateOrganicStories(10, startOffsetMinutes: 0);

        // Act: Enqueue fresh news stories
        var updatedFeed = FeedQueueHelper.EnqueueWithEviction(
            currentFeed: initialFeed,
            incomingStories: freshStories,
            includeAdMobPlaceholders: false,
            maxCapacity: 60);

        // Assert: Sponsored feeds MUST HOLD their exact positions!
        Assert.Equal("sponsor-hero", updatedFeed[0].Id);   // Slot 1 (Index 0) holds!
        Assert.Equal("sponsor-slot-5", updatedFeed[4].Id); // Slot 5 (Index 4) holds!

        // News feeds are arranged around the sponsored positions:
        // Slots 2, 3, 4 (Indices 1, 2, 3) are populated with the newest organic stories
        Assert.Equal("story-1", updatedFeed[1].Id);
        Assert.Equal("story-2", updatedFeed[2].Id);
        Assert.Equal("story-3", updatedFeed[3].Id);

        // Slot 6 (Index 5) resumes with the next newest organic story
        Assert.Equal("story-4", updatedFeed[5].Id);
    }

    [Fact]
    public void SponsoredFeeds_AreImmuneToQueueEviction()
    {
        // Arrange: 1 sponsored story with a very old PublishedAt timestamp
        var oldSponsor = new BriefingItem
        {
            Id = "old-sponsor",
            Title = "Contractual Campaign Sponsor",
            IsSponsored = true,
            TargetPosition = 2,
            PublishedAt = DateTime.UtcNow.AddDays(-30) // 30 days old!
        };

        var existingOrganic = GenerateOrganicStories(58, startOffsetMinutes: 10);
        var currentFeed = FeedQueueHelper.EnqueueWithEviction(
            currentFeed: [],
            incomingStories: [oldSponsor, ..existingOrganic],
            includeAdMobPlaceholders: false,
            maxCapacity: 60);

        // Incoming: 15 brand-new organic stories
        var incoming = GenerateOrganicStories(15, startOffsetMinutes: 0);

        // Act
        var updatedFeed = FeedQueueHelper.EnqueueWithEviction(
            currentFeed: currentFeed,
            incomingStories: incoming,
            includeAdMobPlaceholders: false,
            maxCapacity: 60);

        // Assert: Bounded to 60, and old sponsor is still present!
        Assert.Equal(60, updatedFeed.Count);
        Assert.Contains(updatedFeed, s => s.Id == "old-sponsor");
        Assert.Equal("old-sponsor", updatedFeed[1].Id); // Held slot 2
    }

    [Fact]
    public void CountNewStories_AccuratelyDiffsAgainstActiveFeed()
    {
        // Arrange
        var feed = GenerateOrganicStories(20);

        // 3 stories that are already in the feed, plus 5 truly new stories
        var existingSubset = feed.Take(3).ToList();
        var trulyNew = GenerateOrganicStories(5, startOffsetMinutes: 500);
        var candidates = existingSubset.Concat(trulyNew).ToList();

        // Act
        var newCount = FeedQueueHelper.CountNewStories(feed, candidates);

        // Assert
        Assert.Equal(5, newCount);
    }

    [Fact]
    public void CountNewStories_WhenOnlyNewerThanFeed_ExcludesOlderEvictedStories()
    {
        // Arrange: Active feed with stories published between 10 and 29 minutes ago
        var feed = GenerateOrganicStories(20, startOffsetMinutes: 10);

        // Candidates contain:
        // - 2 stories published 1 to 2 minutes ago (genuinely newer than the top of the feed)
        // - 3 stories published 100 to 102 minutes ago (older stories that were evicted from the tail)
        var newerStories = GenerateOrganicStories(2, startOffsetMinutes: 0);
        var olderEvictedStories = GenerateOrganicStories(3, startOffsetMinutes: 100);
        var candidates = newerStories.Concat(olderEvictedStories).ToList();

        // Act
        var countWithoutFilter = FeedQueueHelper.CountNewStories(feed, candidates, onlyNewerThanFeed: false);
        var countWithFilter = FeedQueueHelper.CountNewStories(feed, candidates, onlyNewerThanFeed: true);

        // Assert: Without filter, all 5 missing stories are counted. With filter, ONLY the 2 newer stories are counted!
        Assert.Equal(5, countWithoutFilter);
        Assert.Equal(2, countWithFilter);
    }
}
