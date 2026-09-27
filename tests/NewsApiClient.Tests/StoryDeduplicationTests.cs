using System;
using System.Collections.Generic;
using System.Linq;
using NigerianNewsGrid.Client.Helpers;
using NigerianNewsGrid.Client.Models;
using Xunit;

namespace NewsApiClient.Tests;

public class StoryDeduplicationTests
{
    [Theory]
    [InlineData(
        "Tinubu orders release of detained minors",
        "Tinubu Directs Immediate Release of Detained Minors - Punch Newspapers")]
    [InlineData(
        "BREAKING: FG to begin payment of new minimum wage this month",
        "New Minimum Wage: FG to commence payment this month | Vanguard News")]
    [InlineData(
        "[JUST IN] Bandits kill 5, abduct 10 in fresh Kaduna attack",
        "5 killed, 10 abducted as bandits attack Kaduna community - Daily Post Nigeria")]
    [InlineData(
        "Osimhen scores brace as Galatasaray beat Tottenham 3-2",
        "Europa League: Victor Osimhen nets twice in Galatasaray win over Spurs")]
    [InlineData(
        "Dangote Refinery begins nationwide petrol distribution to NNPC",
        "NNPC commences petrol lifting from Dangote refinery | TheCable")]
    [InlineData(
        "Court grants Bobrisky N5m bail in naira abuse trial",
        "Bobrisky granted N5m bail over naira mutilation by Federal High Court")]
    public void AreDuplicateStories_DetectsSameStoryFromDifferentSources(string headlineA, string headlineB)
    {
        bool isDuplicate = StoryDeduplicationHelper.AreDuplicateStories(headlineA, headlineB);
        Assert.True(isDuplicate, $"Expected '{headlineA}' and '{headlineB}' to be recognized as duplicate stories of the same event.");
    }

    [Theory]
    [InlineData(
        "Tinubu travels to Paris for bilateral trade summit",
        "Tinubu signs new national minimum wage bill into law")]
    [InlineData(
        "CBN slashes interest rate to 26.5% at MPC meeting",
        "CBN issues fresh cybercrime compliance guidelines for commercial banks")]
    [InlineData(
        "Super Eagles defeat Ghana 2-1 in international friendly",
        "Super Falcons depart Abuja for Paris 2024 Olympic games")]
    [InlineData(
        "House of Representatives passes 2026 budget for second reading",
        "Senate summons security chiefs over rising kidnapping incidents in Abuja")]
    public void AreDuplicateStories_DistinguishesDifferentStoriesWithCommonActors(string headlineA, string headlineB)
    {
        bool isDuplicate = StoryDeduplicationHelper.AreDuplicateStories(headlineA, headlineB);
        Assert.False(isDuplicate, $"Expected '{headlineA}' and '{headlineB}' to be recognized as distinct events despite shared actor names.");
    }

    [Fact]
    public void AreDuplicateStories_HandlesNullOrWhitespaceSafely()
    {
        Assert.False(StoryDeduplicationHelper.AreDuplicateStories(null, "Some headline"));
        Assert.False(StoryDeduplicationHelper.AreDuplicateStories("Some headline", ""));
        Assert.False(StoryDeduplicationHelper.AreDuplicateStories("   ", "   "));
        Assert.False(StoryDeduplicationHelper.AreDuplicateStories(null, null));
    }

    [Fact]
    public void DeduplicateStories_FiltersOutDuplicateCoverageAcrossSources()
    {
        var articles = new List<BriefingItem>
        {
            new()
            {
                Id = "art-1",
                Title = "Tinubu orders release of detained EndBadGovernance minors",
                Source = "Punch",
                Summary = "President directs immediate withdrawal of charges against all minors."
            },
            new()
            {
                Id = "art-2",
                Title = "Tinubu Directs Immediate Release of Detained Minors | Vanguard News",
                Source = "Vanguard",
                Summary = "Federal Government orders AGF to withdraw treason charges against minors."
            },
            new()
            {
                Id = "art-3",
                Title = "CBN reduces interest rate to 26.5% at monetary policy meeting",
                Source = "Daily Post",
                Summary = "The Central Bank has eased benchmark rates."
            },
            new()
            {
                Id = "art-4",
                Title = "Minors' trial: Tinubu directs AGF to drop charges - TheCable",
                Source = "TheCable",
                Summary = "President Tinubu has asked the attorney-general to drop all charges."
            },
            new()
            {
                Id = "art-5",
                Title = "Osimhen scores twice in Galatasaray 3-2 victory over Tottenham",
                Source = "Premium Times",
                Summary = "Super Eagles striker scored two first-half goals."
            }
        };

        var unique = StoryDeduplicationHelper.DeduplicateStories(
            articles,
            a => a.Title,
            a => a.Summary,
            a => a.Source)
            .ToList();

        // art-1 (Tinubu release minors), art-3 (CBN), and art-5 (Osimhen) should remain.
        // art-2 and art-4 are duplicates of art-1 and should be filtered out.
        Assert.Equal(3, unique.Count);
        Assert.Equal("art-1", unique[0].Id);
        Assert.Equal("art-3", unique[1].Id);
        Assert.Equal("art-5", unique[2].Id);
    }

    [Theory]
    [InlineData("protesters", "protest")]
    [InlineData("killed", "kill")]
    [InlineData("orders", "order")]
    [InlineData("attacks", "attack")]
    [InlineData("ministries", "ministry")]
    [InlineData("protesting", "protest")]
    public void StemWord_StemsCommonNewsInflectionsCorrectly(string input, string expected)
    {
        var result = StoryDeduplicationHelper.StemWord(input);
        Assert.Equal(expected, result);
    }
}
