using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NewsApi.Controllers;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Models;
using NewsApi.Services;
using Xunit;

namespace NewsApi.Tests;

public class ArticlesSeoEndpointTests
{
    private NewsDbContext CreateInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new NewsDbContext(options);
    }

    private ArticlesController CreateController(
        NewsDbContext db,
        ISlugService slugService,
        IArticleSeoBackfillService backfillService,
        IConfiguration? config = null)
    {
        var relatedMock = new Mock<IRelatedContentService>();
        var searchMock = new Mock<IArticleSearchService>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var controller = new ArticlesController(
            db,
            relatedMock.Object,
            searchMock.Object,
            cache,
            NullLogger<ArticlesController>.Instance,
            slugService,
            backfillService,
            null,
            null,
            config
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    [Fact]
    public async Task GetArticles_AnonymousCaller_StripsContent()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var slugService = new SlugService();
        var backfillService = new ArticleSeoBackfillService(db, slugService, NullLogger<ArticleSeoBackfillService>.Instance);

        var article = new Article
        {
            Id = "art_pub_1",
            Title = "Public Article Headline",
            Slug = "public-article-headline-123456",
            Excerpt = "This is a brief public teaser.",
            Content = "This is the full proprietary copyrighted news body that must not be exposed.",
            Source = "Premium Times",
            PublishedAt = DateTime.UtcNow
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        var controller = CreateController(db, slugService, backfillService);

        var result = await controller.GetArticles();
        var okResult = Assert.IsType<OkObjectResult>(result);
        var articles = Assert.IsType<List<ArticleDto>>(okResult.Value);

        var returned = Assert.Single(articles);
        Assert.Null(returned.Content);
        Assert.Equal(article.Excerpt, returned.Excerpt);
    }

    [Fact]
    public async Task GetArticles_WithInternalApiKey_PreservesContent()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var slugService = new SlugService();
        var backfillService = new ArticleSeoBackfillService(db, slugService, NullLogger<ArticleSeoBackfillService>.Instance);

        var article = new Article
        {
            Id = "art_priv_1",
            Title = "Internal Article Headline",
            Slug = "internal-article-headline-123456",
            Excerpt = "Internal excerpt.",
            Content = "Full article content required by Scraper, TTS, or internal worker.",
            Source = "Channels TV",
            PublishedAt = DateTime.UtcNow
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        var inMemorySettings = new Dictionary<string, string?>
        {
            {"ApiSecurity:ApiKey", "TestSecretKey123!"}
        };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var controller = CreateController(db, slugService, backfillService, config);
        controller.HttpContext.Request.Headers["X-Api-Key"] = "TestSecretKey123!";

        var result = await controller.GetArticles();
        var okResult = Assert.IsType<OkObjectResult>(result);
        var articles = Assert.IsType<List<ArticleDto>>(okResult.Value);

        var returned = Assert.Single(articles);
        Assert.Equal(article.Content, returned.Content);
    }

    [Fact]
    public async Task GetBySlug_ReturnsOk_WithArticleDetailDto_AndNoRawContent()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var slugService = new SlugService();
        var backfillService = new ArticleSeoBackfillService(db, slugService, NullLogger<ArticleSeoBackfillService>.Instance);

        var article = new Article
        {
            Id = "art_101",
            Title = "Federal Ministry Announces Tech Grant",
            Slug = "federal-ministry-announces-tech-grant-a1b2c3",
            Excerpt = "A ₦500 million technology fund was launched today.",
            Content = "This is the full raw scraped copyrighted body that must never be exposed publicly.",
            Source = "Punch Newspaper",
            Url = "https://punchng.com/tech-grant",
            Category = "Technology",
            PublishedAt = DateTime.UtcNow.AddHours(-3)
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        var controller = CreateController(db, slugService, backfillService);

        var result = await controller.GetBySlug(article.Slug);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var detail = Assert.IsType<ArticleDetailDto>(okResult.Value);

        Assert.Equal(article.Title, detail.Title);
        Assert.Equal(article.Slug, detail.Slug);
        Assert.Equal(article.Excerpt, detail.Excerpt);
        Assert.Equal(article.Source, detail.Source);
        Assert.Equal(article.Url, detail.SourceUrl);

        // Verification of aggregator copyright compliance: detail model must not expose raw Content
        var contentProp = typeof(ArticleDetailDto).GetProperty("Content");
        Assert.Null(contentProp);

        // Header assertions
        var httpContext = controller.HttpContext;
        Assert.True(httpContext.Response.Headers.ContainsKey("ETag"));
        Assert.Contains("public, max-age=60", httpContext.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task GetBySlug_Returns304NotModified_WhenETagMatches()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var slugService = new SlugService();
        var backfillService = new ArticleSeoBackfillService(db, slugService, NullLogger<ArticleSeoBackfillService>.Instance);

        var lastModified = DateTime.UtcNow.AddHours(-1);
        var article = new Article
        {
            Id = "art_102",
            Title = "Central Bank Clarifies Liquidity Guidance",
            Slug = "cbn-clarifies-liquidity-guidance-x9y8z7",
            Excerpt = "Central bank releases updated operational framework.",
            PublishedAt = lastModified,
            UpdatedAt = lastModified
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();

        var expectedETag = $"\"{article.Id}_{lastModified.Ticks}_{article.Slug}\"";

        var controller = CreateController(db, slugService, backfillService);
        controller.HttpContext.Request.Headers["If-None-Match"] = expectedETag;

        var result = await controller.GetBySlug(article.Slug);

        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status304NotModified, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetBySlug_Returns404NotFound_WhenSlugDoesNotExist()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var slugService = new SlugService();
        var backfillService = new ArticleSeoBackfillService(db, slugService, NullLogger<ArticleSeoBackfillService>.Instance);
        var controller = CreateController(db, slugService, backfillService);

        var result = await controller.GetBySlug("non-existent-article-slug");

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ErrorResponseDto>(notFoundResult.Value);
        Assert.Equal("NOT_FOUND", error.Error.Code);
    }
}
