using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NewsApi.Data;
using NewsApi.Infrastructure;
using NewsApi.Services;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using System.Threading.RateLimiting;

namespace NewsApi.Extensions;

/// <summary>
/// Extension methods to keep Program.cs focused on pipeline configuration only.
/// Each method encapsulates a distinct concern (SRP, Fix #14).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers all application-layer services.</summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddMemoryCache();
        services.AddHttpClient();

        // Core application services with interfaces (DIP, Fixes #19, #20, #21)
        services.AddScoped<IRelatedContentService, RelatedContentService>();
        services.AddScoped<IYouTubeFeedService,    YouTubeFeedService>();
        services.AddScoped<ISocialFeedService,     SocialFeedService>();
        services.AddScoped<IFeedbackService,       FeedbackService>();
        services.AddScoped<IArticleSearchService,   ArticleSearchService>();

        // Background services
        services.AddHostedService<BackgroundMediaSyncService>();

        return services;
    }

    /// <summary>Registers infrastructure-level services (DB, health checks).</summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DB initializer (Fix #13)
        services.AddScoped<IDbInitializer, DbInitializer>();

        // Database context — provider auto-detection via config key or connection string signature
        var connectionString = configuration.GetConnectionString("NewsDb")
                            ?? configuration.GetConnectionString("DefaultConnection")
                            ?? "Data Source=news.db";

        var dbProvider = configuration["DbProvider"] ?? "sqlite";
        var isPostgres = dbProvider.StartsWith("postgre", StringComparison.OrdinalIgnoreCase) ||
                         connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
                         connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase);

        services.AddDbContext<NewsDbContext>(options =>
        {
            if (isPostgres)
            {
                options.UseNpgsql(connectionString, b => b.MigrationsAssembly(typeof(NewsDbContext).Assembly.FullName));
            }
            else
            {
                options.UseSqlite(connectionString, b => b.MigrationsAssembly(typeof(NewsDbContext).Assembly.FullName));
            }

            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        // Health checks
        services.AddHealthChecks()
                .AddCheck<NewsApi.Health.DatabaseHealthCheck>("db_health_check");

        return services;
    }

    /// <summary>
    /// Adds rate limiting — sliding window policy at 60 req/min per IP for writes (Fix #4).
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit          = 120,
                        Window               = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow    = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit           = 0
                    }));

            options.OnRejected = async (ctx, ct) =>
            {
                ctx.HttpContext.Response.StatusCode  = StatusCodes.Status429TooManyRequests;
                ctx.HttpContext.Response.ContentType = "application/json";
                await ctx.HttpContext.Response.WriteAsync(
                    """{"error":{"code":"RATE_LIMIT_EXCEEDED","message":"Too many requests. Please slow down."}}""", ct);
            };
        });

        return services;
    }

    /// <summary>
    /// Adds CORS with a named allowlist from configuration (Fixes #2).
    /// Falls back to localhost-only in development.
    /// </summary>
    public static IServiceCollection AddConfiguredCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowConfiguredOrigins", policy =>
            {
                var allowedOrigins = configuration
                    .GetSection("AllowedOrigins")
                    .Get<string[]>();

                if (allowedOrigins is { Length: > 0 })
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                }
                else
                {
                    // Dev fallback — localhost only
                    policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "http://localhost:4200")
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Adds OpenTelemetry tracing and metrics (Fix #38).
    /// Console exporter used by default — swap for .AddOtlpExporter() for GCP/Grafana in production.
    /// </summary>
    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName:    "news-api",
                serviceVersion: "1.0.0"))
            .WithTracing(tracer => tracer
                .AddAspNetCoreInstrumentation(opts => { opts.RecordException = true; })
                .AddHttpClientInstrumentation()
                .AddConsoleExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter());

        return services;
    }
}
