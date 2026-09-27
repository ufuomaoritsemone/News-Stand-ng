using System.Security.Cryptography;
using System.Text;

namespace NewsApi.Middleware;

/// <summary>
/// Fix #7: Validates X-Api-Key header for write/admin endpoints using a Default-Deny policy.
/// Read endpoints (GET, HEAD, OPTIONS) and explicit public mobile client telemetry endpoints
/// remain accessible without an API key.
/// </summary>
public class ApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration config, IHostEnvironment env)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;

        // Non-API paths (e.g. swagger, health checks, static root) bypass API key validation
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Whitelisted public client endpoints for mobile app telemetry & feedback
        if (IsPublicClientEndpoint(path, method))
        {
            await _next(context);
            return;
        }

        // Safe public read GET / HEAD / OPTIONS endpoints that do NOT expose sensitive administrative data
        if ((HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
            && !IsSensitiveAdminEndpoint(path))
        {
            await _next(context);
            return;
        }

        var configuredKey = config["ApiKey"];

        // Hardening Step 3: Fail-closed in Staging / Production if ApiKey is missing
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            if (env.IsProduction() || env.IsStaging())
            {
                _logger.LogCritical("Authentication failure: ApiKey is not configured on this server in {Env} mode.", env.EnvironmentName);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    """{"error":{"code":"SERVICE_UNAVAILABLE","message":"API authentication is not configured for this server."}}""");
                return;
            }

            // In local development only, permit open pass-through with warning
            await _next(context);
            return;
        }

        bool isValid = false;
        if (context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey) &&
            !string.IsNullOrEmpty(providedKey))
        {
            var providedBytes = Encoding.UTF8.GetBytes(providedKey.ToString());
            var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
            isValid = CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
        }

        if (!isValid)
        {
            _logger.LogWarning("Unauthorized {Method} request to {Path} from {IP}. Invalid or missing API key.",
                method, path, context.Connection.RemoteIpAddress);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":{"code":"UNAUTHORIZED","message":"A valid X-Api-Key header is required."}}""");
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Identifies sensitive administrative inspection endpoints that require authentication
    /// even when invoked via GET (e.g. user feedback dumps, raw telemetry events, aggregate metrics).
    /// </summary>
    private static bool IsSensitiveAdminEndpoint(string path)
    {
        if (path.Equals("/api/v1/feedback", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("/api/v1/analytics/summary", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/v1/analytics/events", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Identifies unauthenticated write endpoints required by mobile client apps
    /// for telemetry, interaction analytics, and user feedback submission.
    /// </summary>
    private static bool IsPublicClientEndpoint(string path, string method)
    {
        // Public mobile client user feedback submission (POST only)
        if (HttpMethods.IsPost(method) && path.Equals("/api/v1/feedback", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Public mobile client event telemetry ingest (POST only)
        if (HttpMethods.IsPost(method) && path.Equals("/api/v1/analytics", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Mobile impression and click tracking for articles
        if (HttpMethods.IsPost(method) &&
            path.StartsWith("/api/v1/articles/", StringComparison.OrdinalIgnoreCase) &&
            (path.EndsWith("/track-impression", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith("/track-click", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }
}

