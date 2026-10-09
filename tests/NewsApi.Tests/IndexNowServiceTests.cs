using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NewsApi.Services;
using Xunit;

namespace NewsApi.Tests;

public class TestHttpMessageHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> SentRequests { get; } = [];
    public List<string> RequestBodies { get; } = [];
    public Func<HttpRequestMessage, Task<HttpResponseMessage>> HandlerFunc { get; set; } =
        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SentRequests.Add(request);
        if (request.Content != null)
        {
            var content = await request.Content.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(content);
        }
        return await HandlerFunc(request);
    }
}

public class IndexNowServiceTests
{
    private IConfiguration CreateTestConfig(Dictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["IndexNow:Enabled"] = "true",
            ["IndexNow:Host"] = "newsstand.ng",
            ["IndexNow:Key"] = "3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d",
            ["IndexNow:KeyLocation"] = "https://newsstand.ng/3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d.txt",
            ["IndexNow:Endpoint"] = "https://api.indexnow.org/indexnow"
        };

        if (overrides != null)
        {
            foreach (var kvp in overrides)
            {
                settings[kvp.Key] = kvp.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    [Fact]
    public void GetConfiguration_ReturnsExpectedMetadata()
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var config = CreateTestConfig();
        var service = new IndexNowBackgroundService(httpClient, config, NullLogger<IndexNowBackgroundService>.Instance);

        var (key, host, keyLocation) = service.GetConfiguration();

        Assert.Equal("3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d", key);
        Assert.Equal("newsstand.ng", host);
        Assert.Equal("https://newsstand.ng/3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d.txt", keyLocation);
    }

    [Fact]
    public async Task EnqueueUrlAsync_IsNonBlockingAndAcceptsUrls()
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var config = CreateTestConfig();
        var service = new IndexNowBackgroundService(httpClient, config, NullLogger<IndexNowBackgroundService>.Instance);

        // Enqueueing should complete synchronously/immediately
        var vt1 = service.EnqueueUrlAsync("https://newsstand.ng/news/test-article-111111");
        var vt2 = service.EnqueueUrlsAsync(["https://newsstand.ng/news/test-article-222222", "https://newsstand.ng/news/test-article-333333"]);

        Assert.True(vt1.IsCompletedSuccessfully);
        Assert.True(vt2.IsCompletedSuccessfully);

        await vt1;
        await vt2;
    }

    [Fact]
    public async Task SubmitBatchWithRetryAsync_SendsCorrectPayloadAndEndpoint()
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var config = CreateTestConfig();
        var service = new IndexNowBackgroundService(httpClient, config, NullLogger<IndexNowBackgroundService>.Instance);

        var urls = new List<string>
        {
            "https://newsstand.ng/news/cbn-rate-hike-8f2a1b",
            "https://newsstand.ng/news/dangote-refinery-petrol-supply-9c3d4e"
        };

        var success = await service.SubmitBatchWithRetryAsync(urls, CancellationToken.None);

        Assert.True(success);
        Assert.Single(handler.SentRequests);
        Assert.Equal("https://api.indexnow.org/indexnow", handler.SentRequests[0].RequestUri?.ToString());
        Assert.Equal(HttpMethod.Post, handler.SentRequests[0].Method);

        var requestBody = Assert.Single(handler.RequestBodies);
        using var jsonDoc = JsonDocument.Parse(requestBody);
        var root = jsonDoc.RootElement;

        Assert.Equal("newsstand.ng", root.GetProperty("host").GetString());
        Assert.Equal("3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d", root.GetProperty("key").GetString());
        Assert.Equal("https://newsstand.ng/3a7b9c1d2e4f5a6b7c8d9e0f1a2b3c4d.txt", root.GetProperty("keyLocation").GetString());

        var urlElements = root.GetProperty("urlList").EnumerateArray().Select(u => u.GetString()!).ToList();
        Assert.Equal(urls, urlElements);
    }

    [Fact]
    public async Task SubmitBatchWithRetryAsync_OnTransientError_RetriesAndRecovers()
    {
        var callCount = 0;
        var handler = new TestHttpMessageHandler
        {
            HandlerFunc = _ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call fails with 503 Service Unavailable (transient)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                }
                // Second call succeeds
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        };

        var httpClient = new HttpClient(handler);
        var config = CreateTestConfig();
        var service = new IndexNowBackgroundService(httpClient, config, NullLogger<IndexNowBackgroundService>.Instance);

        var urls = new List<string> { "https://newsstand.ng/news/article-retry-test-112233" };

        var success = await service.SubmitBatchWithRetryAsync(urls, CancellationToken.None, maxRetries: 3);

        Assert.True(success);
        Assert.Equal(2, callCount);
        Assert.Equal(2, handler.SentRequests.Count);
    }

    [Fact]
    public async Task SubmitBatchWithRetryAsync_OnPermanentError_FailsFastWithoutRetry()
    {
        var callCount = 0;
        var handler = new TestHttpMessageHandler
        {
            HandlerFunc = _ =>
            {
                callCount++;
                // 400 Bad Request is permanent (invalid key/format)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
            }
        };

        var httpClient = new HttpClient(handler);
        var config = CreateTestConfig();
        var service = new IndexNowBackgroundService(httpClient, config, NullLogger<IndexNowBackgroundService>.Instance);

        var urls = new List<string> { "https://newsstand.ng/news/article-bad-request-test" };

        var success = await service.SubmitBatchWithRetryAsync(urls, CancellationToken.None, maxRetries: 3);

        Assert.False(success);
        Assert.Equal(1, callCount); // Must not retry non-transient 400 status
    }
}
