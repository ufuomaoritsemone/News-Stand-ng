using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace NewsApi.Tests;

[Collection("NewsApiIntegration")]
public class ImageProxyEndpointTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public ImageProxyEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOptimizedImage_PunchUrl_ReturnsWebp()
    {
        // 1x1 transparent PNG bytes for hermetic test execution
        byte[] pngBytes =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
            0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
            0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
            0x42, 0x60, 0x82
        ];

        var testFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient("ImageProxy")
                    .ConfigurePrimaryHttpMessageHandler(() => new MockImageHandler(pngBytes));
            });
        });

        var client = testFactory.CreateClient();
        var url = "https://cdn.punchng.com/wp-content/uploads/2026/10/03154525/IMG_20261002_172049_013_1791038725-1200x630.webp";
        var response = await client.GetAsync($"/api/v1/img?url={Uri.EscapeDataString(url)}&w=400");
        var content = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Status: {response.StatusCode}, Content: {content}");
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
    }

    private sealed class MockImageHandler(byte[] imageBytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(imageBytes)
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return Task.FromResult(response);
        }
    }
}
