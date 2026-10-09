using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NewsApi.Controllers;
using NewsApi.Data;
using NewsApi.Models;
using Xunit;

namespace NewsApi.Tests;

public class SeoSitemapEndpointTests
{
    private NewsDbContext CreateInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new NewsDbContext(options);
    }

    [Fact]
    public async Task GetSitemapEntries_NewsType_FiltersToRolling48Hours_AndExcludesSponsored()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());

        var recentNews = new Article
        {
            Id = "art_fresh",
            Title = "Recent Breaking News",
            Slug = "recent-breaking-news-111111",
            PublishedAt = DateTime.UtcNow.AddHours(-10),
            IsSponsored = false
        };

        var staleNews = new Article
        {
            Id = "art_stale",
            Title = "Old News Story From Last Week",
            Slug = "old-news-story-222222",
            PublishedAt = DateTime.UtcNow.AddHours(-72),
            IsSponsored = false
        };

        var sponsoredNews = new Article
        {
            Id = "art_sponsored",
            Title = "Commercial Paid Campaign",
            Slug = "commercial-paid-campaign-333333",
            PublishedAt = DateTime.UtcNow.AddHours(-5),
            IsSponsored = true
        };

        db.Articles.AddRange(recentNews, staleNews, sponsoredNews);
        await db.SaveChangesAsync();

        var controller = new SeoController(db, NullLogger<SeoController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetSitemapEntries(type: "news");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SitemapEntriesResponse>(okResult.Value);

        Assert.Equal("news", response.Type);
        Assert.Single(response.Entries);
        Assert.Equal($"/news/{recentNews.Slug}", response.Entries[0].Loc);
        Assert.Equal("News Stand NG", response.Entries[0].PublicationName);
        Assert.Equal("en", response.Entries[0].PublicationLanguage);
    }

    [Fact]
    public async Task GetSitemapEntries_CategoriesType_ReturnsCanonicalCategories()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var controller = new SeoController(db, NullLogger<SeoController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetSitemapEntries(type: "categories");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SitemapEntriesResponse>(okResult.Value);

        Assert.Equal(7, response.TotalCount);
        Assert.Contains(response.Entries, e => e.Loc == "/category/politics");
        Assert.Contains(response.Entries, e => e.Loc == "/category/business");
        Assert.Contains(response.Entries, e => e.Loc == "/category/technology");
    }

    [Fact]
    public async Task GetSitemapEntries_InvalidType_ReturnsBadRequest()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var controller = new SeoController(db, NullLogger<SeoController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetSitemapEntries(type: "unsupported-type");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponseDto>(badRequest.Value);
        Assert.Equal("INVALID_TYPE", error.Error.Code);
    }
}
