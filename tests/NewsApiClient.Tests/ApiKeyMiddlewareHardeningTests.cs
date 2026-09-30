using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NewsApi.Middleware;
using NewsApi.Models;
using Xunit;

namespace NewsApiClient.Tests;

/// <summary>
/// Fix #7: Verifies that NewsApi enforces a secure Default-Deny policy for all write/mutation endpoints
/// while maintaining public access for mobile telemetry, user feedback, and safe GET requests.
/// </summary>
public class ApiKeyMiddlewareHardeningTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiKeyMiddlewareHardeningTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PutArticleCategory_WithoutApiKey_Returns401Unauthorized()
    {
        var content = new StringContent("""{"category":"Business"}""", Encoding.UTF8, "application/json");
        var response = await _client.PutAsync("/api/v1/articles/art-test-123/category", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("UNAUTHORIZED", body);
        Assert.Contains("X-Api-Key", body);
    }

    [Fact]
    public async Task PutArticleCategory_WithInvalidApiKey_Returns401Unauthorized()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/articles/art-test-123/category")
        {
            Content = new StringContent("""{"category":"Business"}""", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Api-Key", "totally-invalid-wrong-key-999");

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PutArticleCategory_WithValidApiKey_PassesAuthentication()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/articles/art-nonexistent-999/category")
        {
            Content = new StringContent("""{"category":"Business"}""", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);

        var response = await _client.SendAsync(request);
        // Authentication succeeded; since the article does not exist, the controller returns 404 NotFound
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostSponsoredArticle_WithoutApiKey_Returns401Unauthorized()
    {
        var content = new StringContent("""{"title":"Sponsored Post"}""", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/articles/sponsored", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSource_WithoutApiKey_Returns401Unauthorized()
    {
        var response = await _client.DeleteAsync("/api/v1/sources/source-123");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteVideoStory_WithoutApiKey_Returns401Unauthorized()
    {
        var response = await _client.DeleteAsync("/api/v1/video-stories/video-456");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAnalytics_WithoutApiKey_IsAllowedForMobileClients()
    {
        // Public mobile client telemetry endpoint should NOT return 401
        var content = new StringContent("[]", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/analytics", content);

        // Expect 400 Bad Request because empty list is provided, but NOT 401 Unauthorized
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostFeedback_WithoutApiKey_IsAllowedForMobileClients()
    {
        // Public mobile client user feedback submission should NOT return 401
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/feedback", content);

        // Expect 400 Bad Request due to validation, but NOT 401 Unauthorized
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostArticleTrackImpression_WithoutApiKey_IsAllowedForMobileClients()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/articles/art-100/track-impression", content);

        // Controller might return 200 or 404 if article doesn't exist, but NOT 401 Unauthorized
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostArticleTrackClick_WithoutApiKey_IsAllowedForMobileClients()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/v1/articles/art-100/track-click", content);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEndpoints_WithoutApiKey_RemainPubliclyAccessible()
    {
        var response = await _client.GetAsync("/api/v1/sources");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SecurityHeaders_AreInjectedOnAllResponses()
    {
        var response = await _client.GetAsync("/api/v1/sources");

        Assert.True(response.Headers.Contains("X-Content-Type-Options"), "Missing X-Content-Type-Options header");
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").FirstOrDefault());

        Assert.True(response.Headers.Contains("X-Frame-Options"), "Missing X-Frame-Options header");
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").FirstOrDefault());

        Assert.True(response.Headers.Contains("Referrer-Policy"), "Missing Referrer-Policy header");
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").FirstOrDefault());

        Assert.True(response.Headers.Contains("Content-Security-Policy"), "Missing Content-Security-Policy header");
    }

    [Fact]
    public async Task GetFeedback_WithoutApiKey_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/v1/feedback");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetFeedback_WithValidApiKey_Returns200OK()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/feedback");
        request.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalyticsSummary_WithoutApiKey_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/v1/analytics/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAnalyticsEvents_WithoutApiKey_Returns401Unauthorized()
    {
        var response = await _client.GetAsync("/api/v1/analytics/events");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadAudio_WithDisallowedExtension_Returns400BadRequest()
    {
        var boundary = $"---------------------------{DateTime.Now.Ticks:x}";
        var form = new MultipartFormDataContent(boundary);
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("malicious script contents"));
        form.Add(fileContent, "file", "malicious_script.php");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/audio/upload")
        {
            Content = form
        };
        request.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("not permitted", body);
    }

    [Fact]
    public async Task UploadAudio_WithPathTraversal_Returns400BadRequest()
    {
        var boundary = $"---------------------------{DateTime.Now.Ticks:x}";
        var form = new MultipartFormDataContent(boundary);
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("audio binary simulation"));
        form.Add(fileContent, "file", "../../../etc/passwd");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/audio/upload")
        {
            Content = form
        };
        request.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddSource_WithInvalidUrl_Returns400BadRequest()
    {
        var payload = """{"name":"Legit Name","rssUrl":"not-a-valid-url-format"}""";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sources")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetSources_ReturnsSourceDtos()
    {
        var response = await _client.GetAsync("/api/v1/sources");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var sources = await response.Content.ReadFromJsonAsync<List<SourceDto>>();
        Assert.NotNull(sources);
        Assert.NotEmpty(sources);
        Assert.All(sources, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Id));
            Assert.False(string.IsNullOrWhiteSpace(s.Name));
        });
    }

    [Fact]
    public async Task GetSources_WithCancelledToken_AbortsGracefully()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _client.GetAsync("/api/v1/sources", cts.Token);
        });
    }

    [Fact]
    public async Task GetVideoChannels_ReturnsVideoChannelDtos()
    {
        var response = await _client.GetAsync("/api/v1/video-channels");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var channels = await response.Content.ReadFromJsonAsync<List<VideoChannelDto>>();
        Assert.NotNull(channels);
    }

    [Fact]
    public async Task GetVideoStories_ReturnsVideoStoryDtos()
    {
        var response = await _client.GetAsync("/api/v1/video-stories?limit=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stories = await response.Content.ReadFromJsonAsync<List<VideoStoryDto>>();
        Assert.NotNull(stories);
    }

    [Fact]
    public async Task GetSocialPosts_ReturnsSocialPostDtos()
    {
        var response = await _client.GetAsync("/api/v1/social-posts?limit=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var posts = await response.Content.ReadFromJsonAsync<List<SocialPostDto>>();
        Assert.NotNull(posts);
    }

    [Fact]
    public async Task GetSocialHandles_ReturnsSocialHandleDtos()
    {
        var response = await _client.GetAsync("/api/v1/social-handles");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var handles = await response.Content.ReadFromJsonAsync<List<SocialHandleDto>>();
        Assert.NotNull(handles);
    }

    [Fact]
    public async Task AddSource_ValidationFailure_DoesNotLeakRawException()
    {
        var payload = """{"name":"","rssUrl":"not-a-valid-url"}""";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/sources")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Api-Key", CustomWebApplicationFactory.TestApiKey);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("at ", body); // stack trace guard
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_WhenResponseHasStarted_LogsAndRethrows()
    {
        var middleware = new GlobalExceptionMiddleware(ctx =>
        {
            throw new InvalidOperationException("Failure occurred mid-stream");
        }, NullLogger<GlobalExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        var mockFeature = new Mock<IHttpResponseFeature>();
        mockFeature.SetupGet(f => f.HasStarted).Returns(true);
        context.Features.Set<IHttpResponseFeature>(mockFeature.Object);

        // Must rethrow original exception rather than masking with 'StatusCode cannot be set'
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
        Assert.Equal("Failure occurred mid-stream", ex.Message);
    }

    [Fact]
    public async Task GlobalExceptionMiddleware_WhenResponseNotStarted_WritesProblemDetails()
    {
        var middleware = new GlobalExceptionMiddleware(ctx =>
        {
            throw new ApplicationException("Unhandled domain fault");
        }, NullLogger<GlobalExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("An unexpected server error occurred", body);
        Assert.DoesNotContain("Unhandled domain fault", body); // Stack and internal message not leaked
    }
}
