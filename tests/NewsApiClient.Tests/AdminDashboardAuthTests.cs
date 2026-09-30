using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NewsApiClient.Tests;

public class AdminDashboardWebApplicationFactory : WebApplicationFactory<AdminDashboard.Program>
{
    public const string TestUser = "admin";
    public const string TestPass = "AdminPass123!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("AdminAuth:Username", TestUser);
        builder.UseSetting("AdminAuth:Password", TestPass);
        builder.UseSetting("ApiBaseUrl", "http://localhost:56193");
    }
}

public class AdminDashboardAuthTests : IClassFixture<AdminDashboardWebApplicationFactory>
{
    private readonly AdminDashboardWebApplicationFactory _factory;

    public AdminDashboardAuthTests(AdminDashboardWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetRoot_WithoutAuthentication_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/Login", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task GetVideos_WithoutAuthentication_RedirectsToLoginWithReturnUrl()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Videos");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/Login?ReturnUrl=%2FVideos", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task GetFeedback_WithoutAuthentication_RedirectsToLoginWithReturnUrl()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/Feedback");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/Login?ReturnUrl=%2FFeedback", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task GetLogin_Anonymous_Returns200OkWithLoginForm()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Admin Portal", content);
        Assert.Contains("Input.Username", content);
        Assert.Contains("Input.Password", content);
    }

    [Fact]
    public async Task PostLogin_InvalidCredentials_FailsAuthentication()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // 1. Fetch login page to capture antiforgery token & cookie
        var getResp = await client.GetAsync("/Login");
        var html = await getResp.Content.ReadAsStringAsync();
        var (token, cookie) = ExtractAntiforgery(getResp, html);

        // 2. Post invalid credentials
        var form = new Dictionary<string, string>
        {
            ["Input.Username"] = "admin",
            ["Input.Password"] = "WrongPassword999!",
            ["__RequestVerificationToken"] = token
        };

        var postReq = new HttpRequestMessage(HttpMethod.Post, "/Login")
        {
            Content = new FormUrlEncodedContent(form)
        };
        if (!string.IsNullOrEmpty(cookie))
        {
            postReq.Headers.Add("Cookie", cookie);
        }

        var postResp = await client.SendAsync(postReq);

        // On failure, it re-renders the login page (200 OK) with error message
        Assert.Equal(HttpStatusCode.OK, postResp.StatusCode);
        var postHtml = await postResp.Content.ReadAsStringAsync();
        Assert.Contains("Invalid administrator username or password.", postHtml);

        // Verify auth cookie is NOT set
        var setCookies = postResp.Headers.TryGetValues("Set-Cookie", out var sc) ? sc : [];
        Assert.DoesNotContain(setCookies, c => c.Contains(".NigerianNewsGrid.AdminAuth"));
    }

    [Fact]
    public async Task PostLogin_ValidCredentials_SetsAuthCookieAndRedirects()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // 1. Fetch login page to capture antiforgery token & cookie
        var getResp = await client.GetAsync("/Login");
        var html = await getResp.Content.ReadAsStringAsync();
        var (token, cookie) = ExtractAntiforgery(getResp, html);

        // 2. Post valid credentials
        var form = new Dictionary<string, string>
        {
            ["Input.Username"] = AdminDashboardWebApplicationFactory.TestUser,
            ["Input.Password"] = AdminDashboardWebApplicationFactory.TestPass,
            ["__RequestVerificationToken"] = token
        };

        var postReq = new HttpRequestMessage(HttpMethod.Post, "/Login")
        {
            Content = new FormUrlEncodedContent(form)
        };
        if (!string.IsNullOrEmpty(cookie))
        {
            postReq.Headers.Add("Cookie", cookie);
        }

        var postResp = await client.SendAsync(postReq);

        // Expect 302 Redirect on successful login
        Assert.Equal(HttpStatusCode.Redirect, postResp.StatusCode);
        var location = postResp.Headers.Location?.ToString();
        Assert.True(location == "/" || location == "/Index");

        // Verify auth cookie IS set
        var setCookies = postResp.Headers.TryGetValues("Set-Cookie", out var sc) ? sc : [];
        Assert.Contains(setCookies, c => c.Contains(".NigerianNewsGrid.AdminAuth"));
    }

    private static (string Token, string Cookie) ExtractAntiforgery(HttpResponseMessage response, string html)
    {
        var match = Regex.Match(html, @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""");
        var token = match.Success ? match.Groups[1].Value : string.Empty;

        var cookieHeader = string.Empty;
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var cookieParts = cookies.Select(c => c.Split(';')[0].Trim());
            cookieHeader = string.Join("; ", cookieParts);
        }

        return (token, cookieHeader);
    }
}
