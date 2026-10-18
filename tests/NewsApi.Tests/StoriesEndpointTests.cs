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

    [Fact]
    public async Task GetTrendingStories_ReturnsRankedStoriesWithScoresAndTopics()
    {
        using var db = CreateInMemoryDb();
        var now = DateTime.UtcNow;

        var topic1 = new Topic { Id = "top-1", Name = "Economy", Slug = "economy" };
        var topic2 = new Topic { Id = "top-2", Name = "China Relations", Slug = "china-relations" };
        db.Topics.AddRange(topic1, topic2);

        // Story 1: 3 sources, updated 1h ago
        var cluster1 = new StoryCluster
        {
            Id = "cluster-trending-1",
            Slug = "tinubu-china-bilateral-summit",
            Title = "President Tinubu in Beijing",
            Summary = "Bilateral talks in Beijing.",
            CreatedAt = now.AddHours(-10),
            UpdatedAt = now.AddHours(-1),
            ArticleCount = 3,
            SourceCount = 3
        };

        // Story 2: 2 sources, updated 5h ago
        var cluster2 = new StoryCluster
        {
            Id = "cluster-trending-2",
            Slug = "cbn-interest-rate-hike",
            Title = "CBN Raises Monetary Policy Rate",
            Summary = "MPC increases benchmark rate.",
            CreatedAt = now.AddHours(-12),
            UpdatedAt = now.AddHours(-5),
            ArticleCount = 2,
            SourceCount = 2
        };

        var a1 = new Article { Id = "art-1", Title = "Tinubu in Beijing", Source = "Punch", Category = "Politics", PublishedAt = now.AddHours(-2) };
        var a2 = new Article { Id = "art-2", Title = "China Welcomes Tinubu", Source = "Vanguard", Category = "Politics", PublishedAt = now.AddHours(-2) };
        var a3 = new Article { Id = "art-3", Title = "Nigeria China Talks", Source = "TheCable", Category = "Politics", PublishedAt = now.AddHours(-1) };

        var a4 = new Article { Id = "art-4", Title = "CBN Rates", Source = "BusinessDay", Category = "Business", PublishedAt = now.AddHours(-6) };
        var a5 = new Article { Id = "art-5", Title = "Cardoso on Rates", Source = "Premium Times", Category = "Business", PublishedAt = now.AddHours(-5) };

        db.Articles.AddRange(a1, a2, a3, a4, a5);
        db.StoryClusters.AddRange(cluster1, cluster2);
        db.StoryClusterArticles.AddRange(
            new StoryClusterArticle { ClusterId = cluster1.Id, ArticleId = a1.Id },
            new StoryClusterArticle { ClusterId = cluster1.Id, ArticleId = a2.Id },
            new StoryClusterArticle { ClusterId = cluster1.Id, ArticleId = a3.Id },
            new StoryClusterArticle { ClusterId = cluster2.Id, ArticleId = a4.Id },
            new StoryClusterArticle { ClusterId = cluster2.Id, ArticleId = a5.Id }
        );

        db.ArticleTopics.Add(new ArticleTopic { ArticleId = a1.Id, TopicId = topic2.Id });
        db.ArticleTopics.Add(new ArticleTopic { ArticleId = a4.Id, TopicId = topic1.Id });

        // Add some user events for art-1
        db.UserEvents.Add(new UserEvent { Id = "ue-1", ArticleId = a1.Id, EventType = "article_read", OccurredAt = now.AddMinutes(-30) });
        db.UserEvents.Add(new UserEvent { Id = "ue-2", ArticleId = a1.Id, EventType = "article_share", OccurredAt = now.AddMinutes(-20) });

        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var actionResult = await controller.GetTrendingStories(limit: 5, windowHours: 24);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<TrendingStoryListResponse>(okResult.Value);

        Assert.Equal(2, response.TotalCount);
        Assert.Equal(2, response.Stories.Count);

        // Cluster 1 should rank higher due to 3 sources + user events + higher recency
        Assert.Equal("tinubu-china-bilateral-summit", response.Stories[0].Slug);
        Assert.True(response.Stories[0].TrendingScore > response.Stories[1].TrendingScore);
        Assert.Contains("China Relations", response.Stories[0].Topics);
        Assert.Contains("Punch", response.Stories[0].Sources);
        Assert.True(controller.Response.Headers.ContainsKey("ETag"));
        Assert.True(controller.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public async Task GetTrendingStories_Supports304NotModified()
    {
        using var db = CreateInMemoryDb();
        var now = DateTime.UtcNow;

        var cluster = new StoryCluster
        {
            Id = "cluster-304",
            Slug = "super-eagles-qualify",
            Title = "Super Eagles qualify for AFCON",
            Summary = "Nigeria books ticket.",
            CreatedAt = now.AddHours(-3),
            UpdatedAt = now.AddHours(-1),
            ArticleCount = 2,
            SourceCount = 2
        };

        var a1 = new Article { Id = "se-1", Title = "Eagles win", Source = "Complete Sports", Category = "Sports", PublishedAt = now.AddHours(-1) };
        var a2 = new Article { Id = "se-2", Title = "Mali defeated", Source = "Punch", Category = "Sports", PublishedAt = now.AddHours(-1) };

        db.Articles.AddRange(a1, a2);
        db.StoryClusters.Add(cluster);
        db.StoryClusterArticles.AddRange(
            new StoryClusterArticle { ClusterId = cluster.Id, ArticleId = a1.Id },
            new StoryClusterArticle { ClusterId = cluster.Id, ArticleId = a2.Id }
        );
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var firstResult = await controller.GetTrendingStories();
        var ok = Assert.IsType<OkObjectResult>(firstResult);
        var eTag = controller.Response.Headers["ETag"].ToString();

        // Now simulate second request with matching ETag
        controller.Request.Headers["If-None-Match"] = eTag;
        var secondResult = await controller.GetTrendingStories();
        var notModified = Assert.IsType<StatusCodeResult>(secondResult);
        Assert.Equal(StatusCodes.Status304NotModified, notModified.StatusCode);
    }

    [Fact]
    public async Task GetTrendingStories_SynthesizesFallback_WhenNoPreexistingClustersExist()
    {
        using var db = CreateInMemoryDb();
        var now = DateTime.UtcNow;

        db.Articles.AddRange(
            new Article
            {
                Id = "fb-1",
                Title = "Senate debates 2026 national fiscal reform bill in Abuja",
                Summary = "Lawmakers reviewed key tax harmonization clauses.",
                Source = "Punch",
                Category = "Politics",
                PublishedAt = now.AddHours(-2),
                Slug = "senate-debates-2026-fiscal-reform"
            },
            new Article
            {
                Id = "fb-2",
                Title = "INEC announces timetable for off-cycle governorship polls",
                Summary = "Electoral commission releases schedule for upcoming state elections.",
                Source = "Vanguard",
                Category = "Politics",
                PublishedAt = now.AddHours(-3),
                Slug = "inec-announces-timetable-polls"
            });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetTrendingStories(limit: 5, windowHours: 24);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<TrendingStoryListResponse>(okResult.Value);

        Assert.NotEmpty(response.Stories);
        Assert.Equal("Politics", response.Stories[0].Category);
        Assert.True(response.Stories[0].SourceCount >= 2);

        // Verify synthesized trend-* slug resolves in GetStoryBySlug
        var slugResult = await controller.GetStoryBySlug(response.Stories[0].Slug);
        var slugOk = Assert.IsType<OkObjectResult>(slugResult);
        var clusterDto = Assert.IsType<StoryClusterDto>(slugOk.Value);
        Assert.Equal(2, clusterDto.Articles.Count);
    }
}
