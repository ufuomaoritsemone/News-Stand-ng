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

public class TopicsEndpointTests
{
    private NewsDbContext CreateInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new NewsDbContext(options);
    }

    [Fact]
    public async Task GetTopicBySlug_ReturnsOk_WithArticleCount()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var slugService = new SlugService();

        var topic = new Topic
        {
            Id = "top_tinubu",
            Slug = "bola-tinubu",
            Name = "Bola Ahmed Tinubu",
            Type = TopicType.Person,
            Description = "President of Nigeria"
        };
        db.Topics.Add(topic);

        var article1 = new Article { Id = "art_1", Title = "President signs bill", Slug = "pres-signs-bill-111111", Category = "Politics" };
        var article2 = new Article { Id = "art_2", Title = "President visits Lagos", Slug = "pres-visits-lagos-222222", Category = "Politics" };
        db.Articles.AddRange(article1, article2);

        db.ArticleTopics.AddRange(
            new ArticleTopic { ArticleId = article1.Id, TopicId = topic.Id, Confidence = 0.95f },
            new ArticleTopic { ArticleId = article2.Id, TopicId = topic.Id, Confidence = 0.88f }
        );
        await db.SaveChangesAsync();

        var controller = new TopicsController(db, slugService, NullLogger<TopicsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetTopicBySlug("bola-tinubu");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<TopicDto>(okResult.Value);

        Assert.Equal("bola-tinubu", dto.Slug);
        Assert.Equal("Bola Ahmed Tinubu", dto.Name);
        Assert.Equal("Person", dto.Type);
        Assert.Equal(2, dto.ArticleCount);
    }

    [Fact]
    public async Task GetTopicBySlug_ReturnsNotFound_WhenTopicDoesNotExist()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var slugService = new SlugService();
        var controller = new TopicsController(db, slugService, NullLogger<TopicsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetTopicBySlug("unknown-topic");

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetTopicArticles_ReturnsPagedStories()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        var slugService = new SlugService();

        var topic = new Topic { Id = "top_cbn", Slug = "cbn", Name = "Central Bank of Nigeria", Type = TopicType.Organization };
        db.Topics.Add(topic);

        var article = new Article
        {
            Id = "art_cbn",
            Title = "CBN holds MPC Meeting",
            Slug = "cbn-holds-mpc-meeting-333333",
            Category = "Business",
            PublishedAt = DateTime.UtcNow
        };
        db.Articles.Add(article);
        db.ArticleTopics.Add(new ArticleTopic { ArticleId = article.Id, TopicId = topic.Id });
        await db.SaveChangesAsync();

        var controller = new TopicsController(db, slugService, NullLogger<TopicsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.GetTopicArticles("cbn", page: 1, pageSize: 10);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var paged = Assert.IsType<PagedResponse<ArticleDto>>(okResult.Value);

        Assert.Equal(1, paged.TotalCount);
        Assert.Single(paged.Items);
        Assert.Equal(article.Title, paged.Items.First().Title);
    }
}
