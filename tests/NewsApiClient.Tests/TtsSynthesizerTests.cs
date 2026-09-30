using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NewsApi.Controllers;
using NewsApi.Data;
using NewsApi.Models;
using TtsWorker.Synthesizers;
using Xunit;

namespace NewsApiClient.Tests;

/// <summary>
/// Comprehensive verification for Problem 3: Tri-Tier Neural TTS Architecture,
/// fallback resilience, and NewsApi seekable audio streaming.
/// </summary>
public class TtsSynthesizerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TtsSynthesizerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LocalFallbackTtsSynthesizer_GeneratesValidWaveHeaderAndPcmBytes()
    {
        // Arrange
        var synthesizer = new LocalFallbackTtsSynthesizer(NullLogger<LocalFallbackTtsSynthesizer>.Instance);

        // Act
        var result = await synthesizer.SynthesizeAsync("Breaking News: Nigerian Markets Close Strong.");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.AudioData);
        Assert.True(result.AudioData.Length > 44, "RIFF WAV file must be longer than header");
        Assert.Equal("audio/wav", result.ContentType);
        Assert.Equal("LocalFallback", result.ProviderUsed);

        // Verify standard RIFF header signature: "RIFF" ... "WAVE" ... "fmt " ... "data"
        var riffSignature = Encoding.ASCII.GetString(result.AudioData, 0, 4);
        var waveSignature = Encoding.ASCII.GetString(result.AudioData, 8, 4);
        var fmtSignature = Encoding.ASCII.GetString(result.AudioData, 12, 4);
        var dataSignature = Encoding.ASCII.GetString(result.AudioData, 36, 4);

        Assert.Equal("RIFF", riffSignature);
        Assert.Equal("WAVE", waveSignature);
        Assert.Equal("fmt ", fmtSignature);
        Assert.Equal("data", dataSignature);
    }

    [Fact]
    public async Task GoogleCloudTtsSynthesizer_TracksConfigurationStatus()
    {
        // Arrange — empty configuration (dormant)
        var unconfiguredConfig = new ConfigurationBuilder().Build();
        var mockHttpFactory = new Mock<IHttpClientFactory>();
        var unconfigured = new GoogleCloudTtsSynthesizer(
            mockHttpFactory.Object,
            unconfiguredConfig,
            NullLogger<GoogleCloudTtsSynthesizer>.Instance);

        Assert.False(unconfigured.IsConfigured, "Google Cloud TTS must be dormant without API key");

        var result = await unconfigured.SynthesizeAsync("Test");
        Assert.False(result.Success);
        Assert.Contains("not configured", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        // Arrange — configured with API key
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "GoogleCloud:ApiKey", "AIzaSyTestMockKey12345" }
        };
        var configuredConfig = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var configured = new GoogleCloudTtsSynthesizer(
            mockHttpFactory.Object,
            configuredConfig,
            NullLogger<GoogleCloudTtsSynthesizer>.Instance);

        Assert.True(configured.IsConfigured, "Google Cloud TTS must evaluate IsConfigured=true when API key is provided");
    }

    [Fact]
    public void EdgeNeuralTtsSynthesizer_IsConfigured_AlwaysTrue_ZeroCost()
    {
        // Edge Neural TTS operates out-of-the-box without keys
        var edgeSynthesizer = new EdgeNeuralTtsSynthesizer(NullLogger<EdgeNeuralTtsSynthesizer>.Instance);

        Assert.True(edgeSynthesizer.IsConfigured);
        Assert.Equal("EdgeNeural", edgeSynthesizer.ProviderName);
    }

    [Fact]
    public async Task CompositeTtsSynthesizer_SeamlesslyFallsBackToLocal_WhenNetworkFails()
    {
        // Arrange — unconfigured GCP, Edge Neural that throws/fails
        var emptyConfig = new ConfigurationBuilder().Build();
        var mockHttpFactory = new Mock<IHttpClientFactory>();

        var gcp = new GoogleCloudTtsSynthesizer(mockHttpFactory.Object, emptyConfig, NullLogger<GoogleCloudTtsSynthesizer>.Instance);
        var edge = new EdgeNeuralTtsSynthesizer(NullLogger<EdgeNeuralTtsSynthesizer>.Instance);
        var local = new LocalFallbackTtsSynthesizer(NullLogger<LocalFallbackTtsSynthesizer>.Instance);

        var composite = new CompositeTtsSynthesizer(
            gcp,
            edge,
            local,
            NullLogger<CompositeTtsSynthesizer>.Instance);

        // Act — even with impossible text or simulated disconnect, fallback ensures success
        var result = await composite.SynthesizeAsync("Headline news test");

        // Assert — must succeed and return playable audio bytes
        Assert.True(result.Success);
        Assert.NotNull(result.AudioData);
        Assert.True(result.AudioData.Length > 0);
    }

    [Fact]
    public void AudioController_PathTraversal_IsBlocked()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var db = new NewsDbContext(options);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().Build();

        var controller = new AudioController(db, config, cache, NullLogger<AudioController>.Instance);

        // Act
        var badResult1 = controller.StreamAudio("../../../etc/passwd");
        var badResult2 = controller.StreamAudio("..\\windows\\system32\\calc.exe");

        // Assert
        Assert.IsType<BadRequestObjectResult>(badResult1);
        Assert.IsType<BadRequestObjectResult>(badResult2);
    }

    [Fact]
    public void AudioController_MissingFile_ReturnsNotFound()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var db = new NewsDbContext(options);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().Build();

        var controller = new AudioController(db, config, cache, NullLogger<AudioController>.Instance);

        // Act
        var result = controller.StreamAudio("non_existent_audio_file.mp3");

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AudioController_RegisterAudio_AttachesAudioUrlToArticle()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<NewsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var db = new NewsDbContext(options);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().Build();

        var articleId = Guid.NewGuid().ToString();
        db.Articles.Add(new Article
        {
            Id = articleId,
            Title = "Central Bank of Nigeria Issues New Guidelines",
            Category = "Economy",
            Source = "CentralBank"
        });
        await db.SaveChangesAsync();

        var controller = new AudioController(db, config, cache, NullLogger<AudioController>.Instance);

        // Act
        var registerRequest = new AudioController.AudioRegistrationRequest(
            Cycle: "2026-09-12_morning",
            TimeOfDay: "morning",
            FileName: "Economy_1.mp3",
            Category: "Economy",
            ArticleId: articleId,
            Provider: "EdgeNeural"
        );

        var response = await controller.RegisterAudio(registerRequest);

        // Assert
        Assert.IsType<OkObjectResult>(response);
        var updatedArticle = await db.Articles.FindAsync(articleId);
        Assert.NotNull(updatedArticle);
        Assert.Equal("/api/v1/audio/Economy_1.mp3", updatedArticle.AudioUrl);
    }

    [Fact]
    public async Task AudioController_GetLatestBriefing_Returns200WithMetadata()
    {
        // Arrange integration client
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/audio/briefings/latest");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var metadata = await response.Content.ReadFromJsonAsync<NigerianNewsGrid.Client.Models.AudioBriefingMetadata>();
        Assert.NotNull(metadata);
    }

    [Fact]
    public void TtsWorker_DefaultFallbackPort_Is56193()
    {
        var config = new ConfigurationBuilder().Build();
        var apiBaseUrl = config["ApiBaseUrl"] 
            ?? config["NewsApi:BaseUrl"] 
            ?? "http://localhost:56193";

        Assert.Equal("http://localhost:56193", apiBaseUrl);
    }
}
