using System;
using System.Collections.Generic;
using System.Linq;
using NigerianNewsGrid.Client.Helpers;
using NigerianNewsGrid.Client.Models;
using Xunit;

namespace NewsApiClient.Tests;

public class FeedSlotPlacementTests
{
    private static List<BriefingItem> GenerateOrganicStories(int count)
    {
        var list = new List<BriefingItem>(count);
        var baseTime = DateTime.UtcNow;
        for (var i = 1; i <= count; i++)
        {
            list.Add(new BriefingItem
            {
                Id = $"organic-{i}",
                Title = $"Organic Story {i}",
                Summary = $"Summary for organic story {i}",
                Source = "National News",
                Category = "General",
                PublishedAt = baseTime.AddMinutes(-i),
                IsSponsored = false,
                IsAdMobPlaceholder = false
            });
        }
        return list;
    }

    [Fact]
    public void SpecialPlacements_PlacedAtExactSlotsOneTwoThree()
    {
        // Arrange
        var organic = GenerateOrganicStories(10);
        var sponsored = new List<BriefingItem>
        {
            new() { Id = "sp-1", Title = "Hero Brand", IsSponsored = true, TargetPosition = 1 },
            new() { Id = "sp-2", Title = "Second Brand", IsSponsored = true, TargetPosition = 2 },
            new() { Id = "sp-3", Title = "Third Brand", IsSponsored = true, TargetPosition = 3 }
        };

        // Act
        var feed = FeedSlotPlacementHelper.ArrangeFeed(organic, sponsored, includeAdMobPlaceholders: false);

        // Assert
        Assert.Equal("sp-1", feed[0].Id); // Slot 1 = index 0
        Assert.Equal("sp-2", feed[1].Id); // Slot 2 = index 1
        Assert.Equal("sp-3", feed[2].Id); // Slot 3 = index 2
        Assert.Equal("organic-1", feed[3].Id); // Slot 4 = index 3 (organic buffer)
        Assert.Equal("organic-2", feed[4].Id); // Slot 5 = index 4
    }

    [Fact]
    public void StandardInFeed_PlacedAtRequestedPositionFive()
    {
        // Arrange
        var organic = GenerateOrganicStories(15);
        var sponsored = new List<BriefingItem>
        {
            new() { Id = "sp-5", Title = "Fintech Sponsor", IsSponsored = true, TargetPosition = 5 }
        };

        // Act
        var feed = FeedSlotPlacementHelper.ArrangeFeed(organic, sponsored, includeAdMobPlaceholders: false);

        // Assert
        // First 4 items are organic stories 1 to 4
        Assert.Equal("organic-1", feed[0].Id);
        Assert.Equal("organic-2", feed[1].Id);
        Assert.Equal("organic-3", feed[2].Id);
        Assert.Equal("organic-4", feed[3].Id);

        // 5th item (index 4) is our placed sponsored story
        Assert.Equal("sp-5", feed[4].Id);
        Assert.True(feed[4].IsSponsored);

        // 6th item onwards resumes organic stories
        Assert.Equal("organic-5", feed[5].Id);
    }

    [Fact]
    public void CollisionHandling_HigherPriorityRetainsSlot_LowerPriorityBumpsToNextSlot()
    {
        // Arrange
        var organic = GenerateOrganicStories(20);
        var sponsored = new List<BriefingItem>
        {
            new()
            {
                Id = "sp-low",
                Title = "Low Priority Sponsor",
                IsSponsored = true,
                TargetPosition = 5,
                PriorityWeight = 1,
                PublishedAt = DateTime.UtcNow.AddMinutes(-10)
            },
            new()
            {
                Id = "sp-high",
                Title = "High Priority Sponsor",
                IsSponsored = true,
                TargetPosition = 5,
                PriorityWeight = 10,
                PublishedAt = DateTime.UtcNow.AddMinutes(-5)
            }
        };

        // Act
        // Separation distance is 0 in this test to test immediate slot bumping from 5 to 6
        var feed = FeedSlotPlacementHelper.ArrangeFeed(organic, sponsored, includeAdMobPlaceholders: false, minSeparation: 0);

        // Assert
        Assert.Equal("sp-high", feed[4].Id); // 5th item = index 4 (High Priority wins)
        Assert.Equal("sp-low", feed[5].Id);  // 6th item = index 5 (Low Priority bumped to 6)
    }

    [Fact]
    public void AdDensitySeparation_EnforcesMinimumOrganicGapBetweenInFeedAds()
    {
        // Arrange
        var organic = GenerateOrganicStories(25);
        var sponsored = new List<BriefingItem>
        {
            new() { Id = "sp-a", Title = "Sponsor A", IsSponsored = true, TargetPosition = 5, PriorityWeight = 5 },
            new() { Id = "sp-b", Title = "Sponsor B", IsSponsored = true, TargetPosition = 6, PriorityWeight = 1 } // requests slot 6, which violates min separation of 3
        };

        // Act
        var feed = FeedSlotPlacementHelper.ArrangeFeed(organic, sponsored, includeAdMobPlaceholders: false, minSeparation: 3);

        // Assert
        var indexA = feed.FindIndex(i => i.Id == "sp-a");
        var indexB = feed.FindIndex(i => i.Id == "sp-b");

        Assert.Equal(4, indexA); // Position 5 = index 4
        // Position 5 + (minSeparation 3 + 1) = Position 9 = index 8
        Assert.True(indexB >= indexA + 4, $"Expected indexB ({indexB}) to be at least {indexA + 4} to respect min separation.");
    }

    [Fact]
    public void AdMobPlaceholders_InjectedAtBenchmarkSlotsWhenNoSponsorConflicts()
    {
        // Arrange
        var organic = GenerateOrganicStories(25);
        var sponsored = new List<BriefingItem>
        {
            new() { Id = "sp-5", Title = "Sponsor at 5", IsSponsored = true, TargetPosition = 5 }
        };

        // Act
        // Default benchmark slots: 10 and 20
        var feed = FeedSlotPlacementHelper.ArrangeFeed(
            organic,
            sponsored,
            includeAdMobPlaceholders: true,
            adMobBenchmarkSlots: [10, 20],
            minSeparation: 3);

        // Assert
        Assert.Equal("sp-5", feed[4].Id); // Position 5 (index 4)

        var adMobCards = feed.Where(i => i.IsAdMobPlaceholder).ToList();
        Assert.NotEmpty(adMobCards);

        // Benchmark slot 10 is index 9
        Assert.True(feed[9].IsAdMobPlaceholder);
    }

    [Fact]
    public void PositionValidation_AcceptsSpecialSlotsAndInFeedSlots()
    {
        Assert.True(FeedSlotPlacementHelper.IsValidTargetPosition(1));
        Assert.True(FeedSlotPlacementHelper.IsValidTargetPosition(2));
        Assert.True(FeedSlotPlacementHelper.IsValidTargetPosition(3));
        Assert.False(FeedSlotPlacementHelper.IsValidTargetPosition(4)); // Organic buffer
        Assert.True(FeedSlotPlacementHelper.IsValidTargetPosition(5));
        Assert.True(FeedSlotPlacementHelper.IsValidTargetPosition(15));
        Assert.True(FeedSlotPlacementHelper.IsValidTargetPosition(30));
        Assert.False(FeedSlotPlacementHelper.IsValidTargetPosition(0));
        Assert.False(FeedSlotPlacementHelper.IsValidTargetPosition(31));
    }

    [Fact]
    public void SparseOrganicFeed_HandlesGracefullyWithoutDroppingAds()
    {
        // Arrange: Only 3 organic stories exist, but sponsor requests Position 10
        var organic = GenerateOrganicStories(3);
        var sponsored = new List<BriefingItem>
        {
            new() { Id = "sp-far", Title = "Far Sponsor", IsSponsored = true, TargetPosition = 10 }
        };

        // Act
        var feed = FeedSlotPlacementHelper.ArrangeFeed(organic, sponsored, includeAdMobPlaceholders: false);

        // Assert
        Assert.Equal(4, feed.Count);
        Assert.Contains(feed, i => i.Id == "sp-far");
    }
}
