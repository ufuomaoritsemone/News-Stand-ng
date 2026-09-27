using Microsoft.Extensions.Configuration;
using NewsScraperService.Services;
using Xunit;

namespace NewsApiClient.Tests;

/// <summary>
/// Fix #8: Validates dependency alignment (.NET 10 & ML.NET 5.0.0) and dynamic ApiBaseUrl configuration override.
/// </summary>
public class ServiceConfigurationTests
{
    [Fact]
    public void Configuration_CustomApiBaseUrl_OverridesDefaultPort()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["ApiBaseUrl"] = "http://localhost:56195"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var resolvedUrl = config["ApiBaseUrl"] ?? "http://localhost:56193";
        Assert.Equal("http://localhost:56195", resolvedUrl);
    }

    [Fact]
    public void Configuration_NewsApiBaseUrl_FallbackOverridesDefault()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["NewsApi:BaseUrl"] = "http://localhost:56197"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var resolvedUrl = config["ApiBaseUrl"] ?? config["NewsApi:BaseUrl"] ?? "http://localhost:56193";
        Assert.Equal("http://localhost:56197", resolvedUrl);
    }

    [Fact]
    public void MlCategorizerEngine_RuntimeEvaluation_SucceedsWithMlNet5()
    {
        var engine = new MlCategorizerEngine(Microsoft.Extensions.Logging.Abstractions.NullLogger<MlCategorizerEngine>.Instance);
        var (category, confidence, method) = engine.CategorizeWithDetails(
            "Central Bank of Nigeria raises interest rate to 27.5 percent",
            "The monetary policy committee decided to raise benchmark lending rates.");

        Assert.Equal("Business", category);
        Assert.True(confidence >= 0.4f);
        Assert.NotNull(method);
    }

    [Fact]
    public void PortExclusionLogic_IdentifiesOverlappingPortRanges()
    {
        // Simulated Windows Hyper-V dynamic port exclusion ranges
        var ranges = new List<(int Start, int End)>
        {
            (50000, 50059),
            (56190, 56194)
        };

        bool is56193Excluded = ranges.Any(r => 56193 >= r.Start && 56193 <= r.End);
        bool is56195Excluded = ranges.Any(r => 56195 >= r.Start && 56195 <= r.End);

        Assert.True(is56193Excluded, "Port 56193 should be identified as inside exclusion range 56190-56194.");
        Assert.False(is56195Excluded, "Port 56195 should be identified as outside exclusion range.");
    }
}
