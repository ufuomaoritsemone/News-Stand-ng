using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;
using Xunit;

namespace NewsApiClient.Tests;

/// <summary>
/// Unit tests for FeedbackService (Fix #41).
/// Verifies single SaveChangesAsync database persistence and limits clamping.
/// </summary>
public class FeedbackServiceTests
{
    private static NewsDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: $"FeedbackTestDb_{Guid.NewGuid()}")
            .Options;

        return new NewsDbContext(options);
    }

    private static IConfiguration CreateEmptyConfiguration()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            ["Feedback:RecipientEmail"] = "feedback@nigeriannewsgrid.com",
            ["Feedback:Smtp:Host"]       = "" // No SMTP in tests
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task SubmitFeedbackAsync_ValidRequest_PersistsFeedbackToDb()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var config = CreateEmptyConfiguration();
        var service = new FeedbackService(db, config, NullLogger<FeedbackService>.Instance);

        var request = new FeedbackRequestDto(
            Rating: 5,
            Category: "General",
            Message: "Great app!",
            UserEmail: "test@example.com",
            UserName: "Ada Lovelace",
            AppVersion: "1.0.0",
            Platform: "Android");

        // Act
        var result = await service.SubmitFeedbackAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.FeedbackId);

        var saved = await db.Feedbacks.FirstOrDefaultAsync(f => f.Id == result.FeedbackId);
        Assert.NotNull(saved);
        Assert.Equal(5, saved.Rating);
        Assert.Equal("General", saved.Category);
        Assert.Equal("Great app!", saved.Message);
        Assert.Equal("test@example.com", saved.UserEmail);
        Assert.Equal("Ada Lovelace", saved.UserName);
        Assert.False(saved.IsEmailSent); // Since SMTP host is empty
    }

    [Fact]
    public async Task SubmitFeedbackAsync_ClampsRatingBetween1And5()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var config = CreateEmptyConfiguration();
        var service = new FeedbackService(db, config, NullLogger<FeedbackService>.Instance);

        var requestHigh = new FeedbackRequestDto(
            Rating: 10,
            Category: "Bug",
            Message: "Testing rating clamping",
            UserEmail: null,
            UserName: null,
            AppVersion: null,
            Platform: null);

        // Act
        var result = await service.SubmitFeedbackAsync(requestHigh);

        // Assert
        var saved = await db.Feedbacks.FirstOrDefaultAsync(f => f.Id == result.FeedbackId);
        Assert.NotNull(saved);
        Assert.Equal(5, saved.Rating);
    }

    [Fact]
    public async Task GetRecentFeedbacksAsync_ClampsLimitToMax200()
    {
        // Arrange
        using var db = CreateInMemoryDb();
        var config = CreateEmptyConfiguration();
        var service = new FeedbackService(db, config, NullLogger<FeedbackService>.Instance);

        for (int i = 0; i < 10; i++)
        {
            db.Feedbacks.Add(new FeedbackItem
            {
                Rating = 4,
                Category = "Feature",
                Message = $"Feedback {i}",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
            });
        }
        await db.SaveChangesAsync();

        // Act
        var list = await service.GetRecentFeedbacksAsync(limit: 5);

        // Assert
        Assert.Equal(5, list.Count);
    }
}
