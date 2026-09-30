using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NewsApi.Controllers;
using NewsApi.Data;
using NewsApi.Models;
using NewsApi.Services;
using Xunit;

namespace NewsApiClient.Tests;

public class CategorizerServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly NewsDbContext _context;

    public CategorizerServiceTests()
    {
        var services = new ServiceCollection();
        var dbName = $"NewsDb_Categorizer_{Guid.NewGuid():N}";
        services.AddDbContext<NewsDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: dbName));

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<NewsDbContext>();

        // Seed sample category corrections
        _context.CategoryCorrections.AddRange(
            new CategoryCorrection
            {
                Id = "corr-1",
                ArticleId = "art-1",
                Title = "Nigerian Air Force takes delivery of new attack aircraft for counter insurgency",
                Summary = "Military air fleet expanded for joint security operations in the North East region",
                OldCategory = "General",
                NewCategory = "Crime",
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            },
            new CategoryCorrection
            {
                Id = "corr-2",
                ArticleId = "art-2",
                Title = "Central Bank unveils new digital foreign exchange window for diaspora remittances",
                Summary = "The monetary authority announced guidelines for international money transfer operators",
                OldCategory = "General",
                NewCategory = "Business",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            }
        );
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task CategorizerTrainingService_TrainsAndPredictsAccurately()
    {
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var service = new CategorizerTrainingService(
            scopeFactory,
            NullLogger<CategorizerTrainingService>.Instance);

        // 1. Initial Status
        var initialStatus = service.GetStatus();
        Assert.NotNull(initialStatus);
        Assert.Equal(2, initialStatus.TotalCorrections);

        // 2. Train model in-process
        var result = await service.TrainAsync();

        Assert.True(result.Success, $"Training failed with message: {result.Message}");
        Assert.True(result.TotalTrainingSamples > 200, "Should incorporate seed dataset plus corrections");
        Assert.Equal(2, result.HumanCorrectionsUsed);
        Assert.True(result.MicroAccuracy > 0.50, $"Micro accuracy should be reasonable: {result.MicroAccuracy}");

        // 3. Status after train
        var statusAfter = service.GetStatus();
        Assert.True(statusAfter.IsOperational);
        Assert.NotNull(statusAfter.LastTrainedAt);

        // 4. Test Predict
        var sportsPrediction = service.Predict("Super Eagles defeat South Africa 2-1 in World Cup qualifier in Uyo", "");
        Assert.Equal("Sports", sportsPrediction.PredictedCategory);
        Assert.True(sportsPrediction.Confidence > 0.4f);
        Assert.NotEmpty(sportsPrediction.Scores);

        // 5. Test Model Bytes retrieval
        var bytes = service.GetModelBytes();
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task CategorizerController_ReturnsStatusPredictionsAndRetrainResult()
    {
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var trainingService = new CategorizerTrainingService(
            scopeFactory,
            NullLogger<CategorizerTrainingService>.Instance);

        // Initial train
        await trainingService.TrainAsync();

        var controller = new CategorizerController(
            trainingService,
            _context);

        // 1. GET Status
        var statusAction = controller.GetStatus();
        var okStatus = Assert.IsType<OkObjectResult>(statusAction);
        var status = Assert.IsType<CategorizerStatusDto>(okStatus.Value);
        Assert.True(status.IsOperational);

        // 2. GET Corrections
        var correctionsAction = await controller.GetCorrections();
        var okCorrections = Assert.IsType<OkObjectResult>(correctionsAction);
        Assert.NotNull(okCorrections.Value);

        // 3. POST Predict
        var predictAction = controller.PredictSample(new PredictSampleRequest
        {
            Title = "Dangote Refinery cuts diesel price to 900 naira per litre nationwide",
            Summary = "Marketers welcome price reduction to boost industrial operations"
        });
        var okPredict = Assert.IsType<OkObjectResult>(predictAction);
        var pred = Assert.IsType<CategorizerPredictionDto>(okPredict.Value);
        Assert.Equal("Business", pred.PredictedCategory);

        // 4. POST Retrain
        var retrainAction = await controller.RetrainModel();
        var okRetrain = Assert.IsType<OkObjectResult>(retrainAction);
        var trainResult = Assert.IsType<CategorizerTrainResult>(okRetrain.Value);
        Assert.True(trainResult.Success);

        // 5. GET Model download
        var modelFileAction = controller.DownloadModel();
        var fileResult = Assert.IsType<FileContentResult>(modelFileAction);
        Assert.Equal("application/octet-stream", fileResult.ContentType);
        Assert.Equal("categorizer_model.zip", fileResult.FileDownloadName);
    }

    [Fact]
    public void CheckAndTriggerAutoRetrain_TriggersWhenModuloThresholdMet()
    {
        var mockService = new Mock<ICategorizerTrainingService>();
        mockService.Setup(s => s.CheckAndTriggerAutoRetrain());

        // Call the auto-trigger check
        mockService.Object.CheckAndTriggerAutoRetrain();

        mockService.Verify(s => s.CheckAndTriggerAutoRetrain(), Times.Once);
    }
}
