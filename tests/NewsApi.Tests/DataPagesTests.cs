using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NewsApi.Controllers;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;
using Xunit;

namespace NewsApi.Tests;

public class DataPagesTests
{
    private NewsDbContext CreateInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new NewsDbContext(options);
    }

    [Fact]
    public void NigerianStateRegistry_Contains37Entities_AndAllGeopoliticalZones()
    {
        var states = NigerianStateRegistry.AllStates;

        // 36 states + 1 FCT
        Assert.Equal(37, states.Count);

        var zones = states.Select(s => s.GeopoliticalZone).Distinct().ToList();
        Assert.Equal(6, zones.Count);
        Assert.Contains("South West", zones);
        Assert.Contains("South South", zones);
        Assert.Contains("South East", zones);
        Assert.Contains("North Central", zones);
        Assert.Contains("North West", zones);
        Assert.Contains("North East", zones);

        var lagos = NigerianStateRegistry.FindBySlug("lagos");
        Assert.NotNull(lagos);
        Assert.Equal("Lagos", lagos.Name);
        Assert.Equal("Ikeja", lagos.Capital);
        Assert.Equal("Centre of Excellence", lagos.Slogan);

        // Verify suffix normalization
        var lagosFromSuffix = NigerianStateRegistry.FindBySlug("lagos-state");
        Assert.NotNull(lagosFromSuffix);
        Assert.Equal("lagos", lagosFromSuffix.Slug);
    }

    [Fact]
    public async Task MarketDataController_ReturnsMetrics_AndResolvesAliases()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        db.MarketDataRecords.AddRange(
            new MarketDataRecord
            {
                Id = 1,
                MetricKey = "naira-fx",
                Title = "Naira Exchange Rate Tracker",
                PrimaryValue = "₦1,510 / $1",
                SecondaryValue = "₦1,535 / $1",
                Change24h = "+1.2% (7d)",
                Unit = "NGN / USD",
                SourceAttribution = "NAFEM / Parallel",
                LastUpdated = DateTime.UtcNow
            },
            new MarketDataRecord
            {
                Id = 2,
                MetricKey = "fuel-prices",
                Title = "Nigeria PMS Petrol Price Tracker",
                PrimaryValue = "₦1,025 / Litre",
                Change24h = "Stable",
                Unit = "NGN / Litre",
                SourceAttribution = "NMDPRA / Market Survey",
                LastUpdated = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();

        var controller = new MarketDataController(db, NullLogger<MarketDataController>.Instance);

        // 1. Get all metrics
        var allResult = await controller.GetAllMetrics();
        var okAllResult = Assert.IsType<OkObjectResult>(allResult.Result);
        var metricsList = Assert.IsAssignableFrom<List<MarketDataDto>>(okAllResult.Value);
        Assert.Equal(2, metricsList.Count);

        // 2. Direct key lookup
        var directResult = await controller.GetMetric("naira-fx");
        var okDirect = Assert.IsType<OkObjectResult>(directResult.Result);
        var directDto = Assert.IsType<MarketDataDto>(okDirect.Value);
        Assert.Equal("naira-fx", directDto.MetricKey);

        // 3. Alias lookup
        var aliasResult = await controller.GetMetric("naira-exchange-rate");
        var okAlias = Assert.IsType<OkObjectResult>(aliasResult.Result);
        var aliasDto = Assert.IsType<MarketDataDto>(okAlias.Value);
        Assert.Equal("naira-fx", aliasDto.MetricKey);

        // 4. Missing key lookup
        var notFoundResult = await controller.GetMetric("crypto-bitcoin");
        Assert.IsType<NotFoundObjectResult>(notFoundResult.Result);
    }

    [Fact]
    public async Task StatesController_ReturnsAllZones_AndDetailsWithArticles()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());

        var lagosTopic = new Topic
        {
            Id = "top_lagos",
            Slug = "lagos",
            Name = "Lagos",
            Type = TopicType.Place,
            Description = "Commercial capital of Nigeria"
        };
        db.Topics.Add(lagosTopic);

        var article = new Article
        {
            Id = "art_lagos_1",
            Title = "Lagos Blue Line Rail expands ridership",
            Slug = "lagos-blue-line-rail-ridership-123456",
            Category = "Business",
            PublishedAt = DateTime.UtcNow
        };
        db.Articles.Add(article);

        db.ArticleTopics.Add(new ArticleTopic
        {
            ArticleId = article.Id,
            TopicId = lagosTopic.Id,
            Confidence = 0.98f
        });
        await db.SaveChangesAsync();

        var controller = new StatesController(db, NullLogger<StatesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // 1. GetStates
        var allResult = await controller.GetStates();
        var okAllResult = Assert.IsType<OkObjectResult>(allResult.Result);
        var zones = Assert.IsAssignableFrom<List<StateZoneGroupDto>>(okAllResult.Value);
        Assert.Equal(6, zones.Count);
        var totalStates = zones.Sum(z => z.States.Count);
        Assert.Equal(37, totalStates);

        // 2. Exact slug match
        var stateResult = await controller.GetStateBySlug("lagos");
        var okStateResult = Assert.IsType<OkObjectResult>(stateResult.Result);
        var stateDetail = Assert.IsType<StateDetailDto>(okStateResult.Value);
        Assert.Equal("lagos", stateDetail.Slug);
        Assert.Equal("Lagos", stateDetail.Name);
        Assert.Equal("Ikeja", stateDetail.Capital);
        Assert.Single(stateDetail.Articles);
        Assert.Equal("Lagos Blue Line Rail expands ridership", stateDetail.Articles[0].Title);

        // 3. Suffix resolution
        var suffixResult = await controller.GetStateBySlug("lagos-state");
        var okSuffixResult = Assert.IsType<OkObjectResult>(suffixResult.Result);
        var suffixDetail = Assert.IsType<StateDetailDto>(okSuffixResult.Value);
        Assert.Equal("lagos", suffixDetail.Slug);

        // 4. Non-existent state
        var notFound = await controller.GetStateBySlug("atlantis");
        Assert.IsType<NotFoundObjectResult>(notFound.Result);
    }

    [Fact]
    public async Task MarketDataController_ReturnsMultiCurrencyFxData_WhenPresent()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var sampleJson = """
        {
          "points": [
            { "date": "2026-10-10", "label": "Oct 10", "primaryValue": 1380.0, "secondaryValue": 1331.19 }
          ],
          "currencies": {
            "USD": { "code": "USD", "name": "US Dollar", "symbol": "$", "parallelBuy": 1370.0, "parallelSell": 1380.0, "officialRate": 1331.19, "officialBuy": 1330.19, "officialSell": 1331.19, "change24h": "+0.35%" },
            "EUR": { "code": "EUR", "name": "Euro", "symbol": "€", "parallelBuy": 1520.0, "parallelSell": 1540.0, "officialRate": 1490.80, "officialBuy": 1489.68, "officialSell": 1490.80, "change24h": "+0.15%" },
            "GBP": { "code": "GBP", "name": "British Pound", "symbol": "£", "parallelBuy": 1825.0, "parallelSell": 1845.0, "officialRate": 1759.16, "officialBuy": 1757.84, "officialSell": 1759.16, "change24h": "+0.20%" },
            "JPY": { "code": "JPY", "name": "Japanese Yen", "symbol": "¥", "parallelBuy": 8.40, "parallelSell": 8.55, "officialRate": 8.41, "officialBuy": 8.40, "officialSell": 8.41, "change24h": "-0.05%" }
          }
        }
        """;

        db.MarketDataRecords.Add(new MarketDataRecord
        {
            Id = 1,
            MetricKey = "naira-fx",
            Title = "Naira Exchange Rate (USD/NGN)",
            PrimaryValue = "₦1,380.00 / $1",
            SecondaryValue = "₦1,331.19 Official NAFEM",
            Unit = "NGN per USD",
            SourceAttribution = "NAFEM & Parallel",
            HistoricalDataJson = sampleJson,
            LastUpdated = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = new MarketDataController(db, NullLogger<MarketDataController>.Instance);
        var result = await controller.GetMetric("naira-fx");
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<MarketDataDto>(okResult.Value);

        Assert.NotNull(dto.Currencies);
        Assert.Equal(4, dto.Currencies.Count);
        Assert.True(dto.Currencies.ContainsKey("USD"));
        Assert.True(dto.Currencies.ContainsKey("EUR"));
        Assert.True(dto.Currencies.ContainsKey("GBP"));
        Assert.True(dto.Currencies.ContainsKey("JPY"));
        Assert.Equal(1370.0, dto.Currencies["USD"].ParallelBuy);
        Assert.Equal(1380.0, dto.Currencies["USD"].ParallelSell);
        Assert.Equal(1331.19, dto.Currencies["USD"].OfficialRate);
        Assert.Single(dto.HistoricalPoints);
    }
}
