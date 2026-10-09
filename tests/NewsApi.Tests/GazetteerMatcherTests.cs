using Microsoft.Extensions.Logging.Abstractions;
using NewsScraperService.Services;
using Xunit;

namespace NewsApi.Tests;

public class GazetteerMatcherTests
{
    private readonly GazetteerEntityExtractor _extractor = new(NullLogger<GazetteerEntityExtractor>.Instance);

    [Fact]
    public async Task ExtractEntities_FindsStateAndPublicFigureAndCity()
    {
        var title = "Lagos State Governor Babajide Sanwo-Olu inspects Red Line Rail project in Ikeja";
        var summary = "The governor reiterated his administration's commitment to urban transportation reform across Lagos.";

        var matches = await _extractor.ExtractEntitiesAsync(title, summary);

        Assert.NotEmpty(matches);
        var slugs = matches.Select(m => m.Slug).ToList();

        Assert.Contains("lagos-state", slugs);
        Assert.Contains("babajide-sanwo-olu", slugs);
        Assert.Contains("ikeja", slugs);

        var lagosMatch = matches.First(m => m.Slug == "lagos-state");
        Assert.True(lagosMatch.Confidence >= 0.90f);
    }

    [Fact]
    public async Task ExtractEntities_FindsFederalAgenciesAndAcronyms()
    {
        var title = "EFCC secures final forfeiture of luxury properties as CBN tightens FX market regulations";
        var matches = await _extractor.ExtractEntitiesAsync(title);

        var slugs = matches.Select(m => m.Slug).ToList();
        Assert.Contains("efcc-nigeria", slugs);
        Assert.Contains("central-bank-of-nigeria", slugs);
    }

    [Fact]
    public async Task ExtractEntities_RespectsWordBoundaries()
    {
        // "Kano" should NOT match "kanoodle"
        var title = "A kanoodle recipe won an international cooking competition in London";
        var matches = await _extractor.ExtractEntitiesAsync(title);

        var slugs = matches.Select(m => m.Slug).ToList();
        Assert.DoesNotContain("kano-state", slugs);
    }

    [Fact]
    public async Task ExtractEntities_EmptyInput_ReturnsEmptyList()
    {
        var matches = await _extractor.ExtractEntitiesAsync("");
        Assert.Empty(matches);
    }
}
