using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NewsApi.Controllers;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;
using NewsApi.Services.Embeddings;
using Xunit;

namespace NewsApi.Tests;

public class StoriesEndpointTests
{
    private NewsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new NewsDbContext(options);
    }

    private StoriesController CreateController(NewsDbContext db)
    {
        var config = new ConfigurationBuilder().Build();
        var embeddingProvider = new LocalFallbackEmbeddingProvider();
        var slugService = new SlugService();
        var clusteringService = new StoryClusteringService(
            db,
            embeddingProvider,
            slugService,
            config,
            NullLogger<StoryClusteringService>.Instance);

        var controller = new StoriesController(
            db,
            clusteringService,
            config,
            NullLogger<StoriesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    [Fact]
    public async Task GetStories_ReturnsActiveClustersWithPaginationAndETag()
    {
        using var db = CreateInMemoryDb();
        var now = DateTime.UtcNow;

        var cluster = new StoryCluster
        {
            Id = "cluster-1",
            Slug = "tinubu-china-bilateral-summit",
            Title = "President Tinubu arrives in Beijing for China-Africa Summit",
            Summary = "Bilateral discussions centered on infrastructure and digital economy cooperation.",
            CreatedAt = now.AddHours(-10),
            UpdatedAt = now.AddHours(-1),
            ArticleCount = 3,
            SourceCount = 3
        };

        var a1 = new Article { Id = "art-1", Title = "Tinubu in Beijing", Source = "Punch", Slug = "tinubu-in-beijing" };
        var a2 = new Article { Id = "art-2", Title = "China Welcomes Tinubu", Source = "Vanguard", Slug = "china-welcomes-tinubu" };
        var a3 = new Article { Id = "art-3", Title = "Nigeria China Talks", Source = "TheCable", Slug = "nigeria-china-talks" };

        db.Articles.AddRange(a1, a2, a3);
        db.StoryClusters.Add(cluster);
        db.StoryClusterArticles.AddRange(
            new StoryClusterArticle { ClusterId = cluster.Id, ArticleId = a1.Id },
            new StoryClusterArticle { ClusterId = cluster.Id, ArticleId = a2.Id },
            new StoryClusterArticle { ClusterId = cluster.Id, ArticleId = a3.Id }
        );
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var actionResult = await controller.GetStories();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<StoryClusterListResponse>(okResult.Value);

        Assert.Equal(1, response.TotalCount);
        Assert.Single(response.Stories);
        Assert.Equal("tinubu-china-bilateral-summit", response.Stories[0].Slug);
        Assert.Equal(3, response.Stories[0].SourceCount);
        Assert.Contains("Punch", response.Stories[0].Sources);
        Assert.Contains("Vanguard", response.Stories[0].Sources);
        Assert.Contains("TheCable", response.Stories[0].Sources);

        Assert.True(controller.Response.Headers.ContainsKey("ETag"));
        Assert.True(controller.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public async Task GetStoryBySlug_ValidSlug_ReturnsClusterWithArticles()
    {
        using var db = CreateInMemoryDb();
        var now = DateTime.UtcNow;

        var cluster = new StoryCluster
        {
            Id = "cluster-2",
            Slug = "cbn-interest-rate-hike",
            Title = "CBN Raises Monetary Policy Rate to 27.25%",
            Summary = "The monetary policy committee adjusted benchmark rates to anchor inflation expectations.",
            CreatedAt = now.AddHours(-5),
            UpdatedAt = now.AddHours(-1),
            ArticleCount = 2,
            SourceCount = 2
        };

        var a1 = new Article
        {
            Id = "cbn-art-1",
            Title = "CBN Raises MPR by 50bps",
            Source = "BusinessDay",
            Slug = "cbn-raises-mpr-50bps",
            PublishedAt = now.AddHours(-5),
            Url = "https://businessday.ng/cbn-mpr"
        };
        var a2 = new Article
        {
            Id = "cbn-art-2",
            Title = "Cardoso Explains Rate Adjustment",
            Source = "Premium Times",
            Slug = "cardoso-explains-rate-adjustment",
            PublishedAt = now.AddHours(-4),
            Url = "https://premiumtimesng.com/cardoso-rates"
        };

        db.Articles.AddRange(a1, a2);
        db.StoryClusters.Add(cluster);
        db.StoryClusterArticles.AddRange(
            new StoryClusterArticle { ClusterId = cluster.Id, ArticleId = a1.Id, SimilarityScore = 0.88f },
            new StoryClusterArticle { ClusterId = cluster.Id, ArticleId = a2.Id, SimilarityScore = 0.84f }
        );
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var actionResult = await controller.GetStoryBySlug("cbn-interest-rate-hike");

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var dto = Assert.IsType<StoryClusterDto>(okResult.Value);

        Assert.Equal("cbn-interest-rate-hike", dto.Slug);
        Assert.Equal(2, dto.ArticleCount);
        Assert.Equal(2, dto.Articles.Count);
        Assert.Equal("BusinessDay", dto.Articles[0].Source);
        Assert.Equal("Premium Times", dto.Articles[1].Source);
    }

    [Fact]
    public async Task GetStoryBySlug_NotFound_Returns404()
    {
        using var db = CreateInMemoryDb();
        var controller = CreateController(db);

        var actionResult = await controller.GetStoryBySlug("non-existent-story-slug");
        var notFound = Assert.IsType<NotFoundObjectResult>(actionResult);
        var err = Assert.IsType<ErrorResponseDto>(notFound.Value);
        Assert.Equal("NOT_FOUND", err.Error.Code);
    }
}
