using System;
using System.Collections.Generic;
using System.Linq;
using NigerianNewsGrid.Client.Models;
using NigerianNewGrid.Models;
using Xunit;

namespace NewsApiClient.Tests;

public class DateGroupingTests
{
    [Fact]
    public void BuildDateGroups_GroupsArticlesByDateAndOrdersChronologically()
    {
        // Arrange
        var today = DateTime.Today.AddHours(14);
        var yesterday = today.AddDays(-1);
        var threeDaysAgo = today.AddDays(-3);

        var stories = new List<BriefingItem>
        {
            new() { Id = "1", Title = "Today Story 1", PublishedAt = today.AddHours(2) },
            new() { Id = "2", Title = "Today Story 2 (Earlier)", PublishedAt = today.AddHours(-2) },
            new() { Id = "3", Title = "Yesterday Story 1", PublishedAt = yesterday },
            new() { Id = "4", Title = "Old Story 1", PublishedAt = threeDaysAgo }
        };

        // Act
        var groups = BriefingDateGroup.BuildDateGroups(stories);

        // Assert
        Assert.Equal(3, groups.Count);

        // Verify Today group
        var todayGroup = groups[0];
        Assert.Equal("Today", todayGroup.RelativeLabel);
        Assert.Contains("Today", todayGroup.DateHeader);
        Assert.Equal(2, todayGroup.Stories.Count);
        Assert.Equal("1", todayGroup.Stories[0].Id); // Most recent first
        Assert.Equal("2", todayGroup.Stories[1].Id);

        // Verify Yesterday group
        var yesterdayGroup = groups[1];
        Assert.Equal("Yesterday", yesterdayGroup.RelativeLabel);
        Assert.Contains("Yesterday", yesterdayGroup.DateHeader);
        Assert.Single(yesterdayGroup.Stories);
        Assert.Equal("3", yesterdayGroup.Stories[0].Id);

        // Verify Older group
        var olderGroup = groups[2];
        Assert.False(string.IsNullOrWhiteSpace(olderGroup.RelativeLabel));
        Assert.Single(olderGroup.Stories);
        Assert.Equal("4", olderGroup.Stories[0].Id);
    }

    [Fact]
    public void BuildDateGroups_EmptyList_ReturnsEmptyGroupList()
    {
        // Act
        var groups = BriefingDateGroup.BuildDateGroups([]);

        // Assert
        Assert.NotNull(groups);
        Assert.Empty(groups);
    }

    [Fact]
    public void BuildDateGroups_NullPublishedAt_DefaultsToTodayGroup()
    {
        // Arrange
        var stories = new List<BriefingItem>
        {
            new() { Id = "1", Title = "No Date Story", PublishedAt = null }
        };

        // Act
        var groups = BriefingDateGroup.BuildDateGroups(stories);

        // Assert
        Assert.Single(groups);
        Assert.Equal("Today", groups[0].RelativeLabel);
        Assert.Single(groups[0].Stories);
    }

    [Fact]
    public void BriefingItem_TimeAgoText_FormatsAccurately()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var justNow = new BriefingItem { Id = "1", Title = "T1", PublishedAt = now.AddSeconds(-20) };
        var minutesAgo = new BriefingItem { Id = "2", Title = "T2", PublishedAt = now.AddMinutes(-25) };
        var hoursAgo = new BriefingItem { Id = "3", Title = "T3", PublishedAt = now.AddHours(-3) };
        var daysAgo = new BriefingItem { Id = "4", Title = "T4", PublishedAt = now.AddDays(-4) };
        var noDate = new BriefingItem { Id = "5", Title = "T5", PublishedAt = null };

        // Assert
        Assert.Equal("just now", justNow.TimeAgoText);
        Assert.Equal("25m ago", minutesAgo.TimeAgoText);
        Assert.Equal("3h ago", hoursAgo.TimeAgoText);
        Assert.Equal("4d ago", daysAgo.TimeAgoText);
        Assert.Empty(noDate.TimeAgoText);
    }
}
