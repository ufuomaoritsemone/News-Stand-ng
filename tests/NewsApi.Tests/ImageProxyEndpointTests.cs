using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NewsApi.Tests;

public class ImageProxyEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ImageProxyEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOptimizedImage_PunchUrl_ReturnsWebp()
    {
        var client = _factory.CreateClient();
        var url = "https://cdn.punchng.com/wp-content/uploads/2026/10/03154525/IMG_20261002_172049_013_1791038725-1200x630.webp";
        var response = await client.GetAsync($"/api/v1/img?url={Uri.EscapeDataString(url)}&w=400");
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Status: {response.StatusCode}, Content: {content}");
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
    }
}
