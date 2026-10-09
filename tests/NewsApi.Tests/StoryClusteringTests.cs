using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;
using NewsApi.Services.Embeddings;
using Xunit;

namespace NewsApi.Tests;

public class StoryClusteringTests
{
    private NewsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new NewsDbContext(options);
    }

    [Fact]
    public async Task RunClustering_FormsCluster_WhenHighSimilarityAndDistinctSources()
    {
        using var db = CreateInMemoryDb();
        var embeddingProvider = new LocalFallbackEmbeddingProvider();
        var slugService = new SlugService();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Clustering:SimilarityThreshold"] = "0.70"
        }).Build();

        var service = new StoryClusteringService(
            db,
            embeddingProvider,
            slugService,
            config,
            NullLogger<StoryClusteringService>.Instance);

        // Add 2 articles covering the same story from 2 distinct sources
        var now = DateTime.UtcNow;
        var art1 = new Article
        {
            Id = "art-1",
            Title = "Dangote Refinery commences direct supply of PMS to Nigerian fuel stations",
            Summary = "Dangote petroleum refinery has begun loading tankers with petrol for nationwide distribution.",
            Source = "Punch",
            Category = "Business",
            PublishedAt = now.AddHours(-2),
            Slug = "dangote-refinery-commences-supply-pms"
        };

        var art2 = new Article
        {
            Id = "art-2",
            Title = "Dangote Refinery begins nationwide distribution of petrol to fuel marketers",
            Summary = "The Dangote petroleum refinery started loading petrol into tankers for supply across Nigeria today.",
            Source = "Vanguard",
            Category = "Business",
            PublishedAt = now.AddHours(-1),
            Slug = "dangote-refinery-nationwide-distribution-marketers"
        };

        // Add 1 unrelated article
        var art3 = new Article
        {
            Id = "art-3",
            Title = "Super Eagles secure victory over Benin Republic in AFCON qualifiers match",
            Summary = "Ademola Lookman scored twice as Nigeria defeated Benin in Uyo.",
            Source = "Premium Times",
            Category = "Sports",
            PublishedAt = now.AddHours(-3),
            Slug = "super-eagles-secure-victory-benin"
        };

        db.Articles.AddRange(art1, art2, art3);
        await db.SaveChangesAsync();

        var count = await service.RunClusteringAsync();

        Assert.Equal(1, count);

        var clusters = await db.StoryClusters
            .Include(sc => sc.ClusterArticles)
            .ToListAsync();

        Assert.Single(clusters);
        var cluster = clusters[0];
        Assert.Equal(2, cluster.ArticleCount);
        Assert.Equal(2, cluster.SourceCount);
        Assert.Contains(cluster.ClusterArticles, ca => ca.ArticleId == "art-1");
        Assert.Contains(cluster.ClusterArticles, ca => ca.ArticleId == "art-2");
        Assert.DoesNotContain(cluster.ClusterArticles, ca => ca.ArticleId == "art-3");
    }

    [Fact]
    public async Task RunClustering_DoesNotCluster_WhenSingleSourceOnly()
    {
        using var db = CreateInMemoryDb();
        var embeddingProvider = new LocalFallbackEmbeddingProvider();
        var slugService = new SlugService();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Clustering:SimilarityThreshold"] = "0.70"
        }).Build();

        var service = new StoryClusteringService(
            db,
            embeddingProvider,
            slugService,
            config,
            NullLogger<StoryClusteringService>.Instance);

        var now = DateTime.UtcNow;
        // Two similar articles, but both from the SAME source
        var art1 = new Article
        {
            Id = "art-single-1",
            Title = "President Tinubu arrives in Beijing for high-level bilateral trade summit",
            Summary = "The president landed in China for trade discussions with Chinese authorities.",
            Source = "Punch",
            Category = "Politics",
            PublishedAt = now.AddHours(-4),
            Slug = "tinubu-arrives-beijing-trade-summit"
        };

        var art2 = new Article
        {
            Id = "art-single-2",
            Title = "Tinubu lands in China for state visit and bilateral economic discussions",
            Summary = "President Bola Tinubu has touched down in Beijing for official economic talks.",
            Source = "Punch", // SAME SOURCE
            Category = "Politics",
            PublishedAt = now.AddHours(-2),
            Slug = "tinubu-lands-china-state-visit"
        };

        db.Articles.AddRange(art1, art2);
        await db.SaveChangesAsync();

        var count = await service.RunClusteringAsync();

        // Should NOT form a cluster because SourceCount < 2
        Assert.Equal(0, count);
        var clusters = await db.StoryClusters.ToListAsync();
        Assert.Empty(clusters);
    }
}
