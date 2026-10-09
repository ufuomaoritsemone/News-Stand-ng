using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;
using Xunit;

namespace NewsApi.Tests;

public class ArticleSeoBackfillTests
{
    private NewsDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new NewsDbContext(options);
    }

    [Fact]
    public async Task BackfillArticleSeo_AssignsSlugsAndExcerpts_Idempotently()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var slugService = new SlugService();
        var backfillService = new ArticleSeoBackfillService(db, slugService, NullLogger<ArticleSeoBackfillService>.Instance);

        // Seed sources and articles missing SEO fields (simulating legacy data prior to migration)
        db.AutoGenerateSlugsOnSave = false;
        db.Sources.AddRange(
            new Source { Id = "punch", Name = "Punch Newspapers" },
            new Source { Id = "vanguard", Name = "Vanguard News" }
        );

        db.Articles.AddRange(
            new Article
            {
                Id = "art_1",
                Title = "First Major Economic Review",
                Content = "The economy grew by 3.2% in the second quarter. Inflation showed marginal moderation across staple goods.",
                Category = "Business"
            },
            new Article
            {
                Id = "art_2",
                Title = "Super Eagles Win Friendly Encounter",
                Content = "Nigeria triumphed over their opponents in a thrilling 2-1 friendly encounter in Rabat.",
                Category = "Sports"
            }
        );
        await db.SaveChangesAsync();
        db.AutoGenerateSlugsOnSave = true;

        // Run initial backfill
        var result1 = await backfillService.BackfillArticleSeoAsync(500);

        Assert.Equal(2, result1.ArticlesProcessed);
        Assert.Equal(2, result1.SlugsGenerated);
        Assert.Equal(2, result1.ExcerptsGenerated);
        Assert.Equal(2, result1.SourcesProcessed);
        Assert.Equal(2, result1.SourceSlugsGenerated);

        var punch = await db.Sources.FirstAsync(s => s.Id == "punch");
        Assert.Equal("punch-newspapers", punch.Slug);

        var art1 = await db.Articles.FirstAsync(a => a.Id == "art_1");
        Assert.StartsWith("first-major-economic-review-", art1.Slug);
        Assert.False(string.IsNullOrWhiteSpace(art1.Excerpt));

        // Second run: verify idempotency
        var result2 = await backfillService.BackfillArticleSeoAsync(500);
        Assert.Equal(0, result2.ArticlesProcessed);
        Assert.Equal(0, result2.SlugsGenerated);
        Assert.Equal(0, result2.SourcesProcessed);
    }
}
