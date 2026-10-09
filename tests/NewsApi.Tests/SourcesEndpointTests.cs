using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NewsApi.Controllers;
using NewsApi.Data;
using NewsApi.Models;
using Xunit;

namespace NewsApi.Tests;

public class SourcesEndpointTests
{
    private NewsDbContext CreateInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new NewsDbContext(options);
    }

    private SourcesController CreateController(NewsDbContext db)
    {
        var httpFactoryMock = new Mock<IHttpClientFactory>();
        return new SourcesController(db, httpFactoryMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task GetSourceBySlug_ReturnsKnownPublisher_WithBiographicalDetails()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());

        // Add 2 punch articles to verify count
        db.Articles.AddRange(
            new Article
            {
                Id = "p_1",
                Title = "Punch Story 1",
                Source = "Punch",
                Slug = "punch-story-1",
                Url = "https://punchng.com/1"
            },
            new Article
            {
                Id = "p_2",
                Title = "Punch Story 2",
                Source = "Punch",
                Slug = "punch-story-2",
                Url = "https://punchng.com/2"
            }
        );
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.GetSourceBySlug("punch");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var source = Assert.IsType<SourceDto>(okResult.Value);

        Assert.Equal("punch", source.Slug);
        Assert.Equal("Punch Newspaper", source.Name);
        Assert.Equal("https://punchng.com", source.WebsiteUrl);
        Assert.False(string.IsNullOrWhiteSpace(source.Description));
        Assert.Equal(2, source.ArticleCount);
    }

    [Fact]
    public async Task GetSourceBySlug_Returns404_WhenUnknownPublisher()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var controller = CreateController(db);

        var result = await controller.GetSourceBySlug("completely-unknown-news-outlet-xyz");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetSourceBySlug_ReturnsDynamicSource_WhenInDatabase()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        db.Sources.Add(new Source
        {
            Id = "src_custom",
            Name = "Abuja Gazette",
            Slug = "abuja-gazette",
            RssUrl = "https://abujagazette.ng/feed"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.GetSourceBySlug("abuja-gazette");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var source = Assert.IsType<SourceDto>(okResult.Value);

        Assert.Equal("abuja-gazette", source.Slug);
        Assert.Equal("Abuja Gazette", source.Name);
    }
}
