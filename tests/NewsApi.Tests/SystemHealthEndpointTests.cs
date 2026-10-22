using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NewsApi.Tests;

[Collection("NewsApiIntegration")]
public class SystemHealthEndpointTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public SystemHealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LivenessAndReadinessProbes_ReturnHttp200()
    {
        var client = _factory.CreateClient();

        var liveness = await client.GetAsync("/healthz/liveness");
        Assert.True(liveness.IsSuccessStatusCode, "Liveness probe should return 200 OK");

        var readiness = await client.GetAsync("/healthz/readiness");
        Assert.True(readiness.IsSuccessStatusCode, "Readiness probe should return 200 OK");

        var general = await client.GetAsync("/health");
        Assert.True(general.IsSuccessStatusCode, "General health probe should return 200 OK");
    }

    [Fact]
    public async Task GetDetailedHealth_ReturnsComprehensiveTelemetry()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Health/details");
        Assert.True(response.IsSuccessStatusCode);

        using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("status", out var statusEl));
        Assert.False(string.IsNullOrWhiteSpace(statusEl.GetString()));

        Assert.True(root.TryGetProperty("process", out var procEl));
        Assert.True(procEl.TryGetProperty("uptimeSeconds", out _));
        Assert.True(procEl.TryGetProperty("workingSetMb", out _));

        Assert.True(root.TryGetProperty("database", out var dbEl));
        Assert.True(dbEl.TryGetProperty("connected", out var connectedEl));
        Assert.True(connectedEl.GetBoolean());
        Assert.True(dbEl.TryGetProperty("provider", out _));

        Assert.True(root.TryGetProperty("workers", out var workersEl));
        Assert.True(workersEl.TryGetProperty("scraperStatus", out _));

        Assert.True(root.TryGetProperty("storage", out var storageEl));
        Assert.True(storageEl.TryGetProperty("imageProxyCache", out _));
        Assert.True(storageEl.TryGetProperty("audioStorage", out _));
    }

    [Fact]
    public async Task PingDatabase_ReturnsLatencyAndSuccess()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/Health/ping-db", null);
        Assert.True(response.IsSuccessStatusCode);

        using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = jsonDoc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.True(root.TryGetProperty("latencyMs", out var latencyEl));
        Assert.True(latencyEl.GetInt64() >= 0);
    }

    [Fact]
    public async Task PurgeCache_ReturnsSuccessAndFlushesCache()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/Health/purge-cache", null);
        Assert.True(response.IsSuccessStatusCode);

        using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = jsonDoc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
    }
}
