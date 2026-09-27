using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using NewsApi.Extensions;
using NewsApi.Infrastructure;
using NewsApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ── Service Registration ──────────────────────────────────────────────────────
// Each AddX method owns a single concern (SRP, Fix #14)
builder.Services
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration)
    .AddConfiguredCors(builder.Configuration)   // Fix #2 — named origin allowlist
    .AddApiRateLimiting()                        // Fix #4 — sliding window rate limiter
    .AddObservability();                         // Fix #38 — OpenTelemetry tracing + metrics

// Authorization Services (Hardening Step 3)
builder.Services.AddAuthorization();

// RFC 9457 Problem Details (Hardening Step 6)
builder.Services.AddProblemDetails();

var app = builder.Build();

// ── Middleware Pipeline ───────────────────────────────────────────────────────
app.UseMiddleware<GlobalExceptionMiddleware>();

// Transport & Network Security (Hardening Step 2)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

// Security Headers Injection (Hardening Step 2)
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; frame-ancestors 'none';");
    context.Response.Headers.Append("Permissions-Policy", "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");
    await next();
});

app.UseMiddleware<ApiKeyMiddleware>();           // Fix #1/#3 — protect write endpoints
app.UseRateLimiter();                           // Fix #4

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowConfiguredOrigins");          // Fix #2 — named policy
app.UseAuthorization();
app.MapControllers();

// ── Health Checks ─────────────────────────────────────────────────────────────
app.MapHealthChecks("/healthz/liveness",  new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/healthz/readiness", new HealthCheckOptions { Predicate = c => c.Name == "db_health_check" });
app.MapHealthChecks("/health");

// ── Audio Storage Initialisation ──────────────────────────────────────────────
// Fix #3: Ensures data/audio directory is present for seekable HTTP 206 streaming
var audioFolder = app.Configuration["Tts:AudioStoragePath"] 
    ?? app.Configuration["Tts__AudioStoragePath"] 
    ?? Path.Combine(AppContext.BaseDirectory, "data", "audio");
Directory.CreateDirectory(audioFolder);

// ── Database Initialisation ───────────────────────────────────────────────────
// Fix #8: MigrateAsync replaces EnsureCreated + raw DDL blocks
// Fix #9: all DB exceptions are properly logged
// Fix #10: all startup queries are async
// Fix #13: seeding delegated to DbInitializer
// Fix #29: no more fire-and-forget Task.Run — BackgroundMediaSyncService handles startup sync
using (var scope = app.Services.CreateScope())
{
    var initializer   = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
    var logger        = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Startup Security Configuration Guard (Hardening Step 1)
    var configuredApiKey = app.Configuration["ApiKey"];
    var ytApiKey = app.Configuration["YouTube:ApiKey"] ?? Environment.GetEnvironmentVariable("YOUTUBE_API_KEY");

    if (string.IsNullOrWhiteSpace(configuredApiKey))
    {
        if (app.Environment.IsProduction())
        {
            logger.LogError("CRITICAL SECURITY RISK: ApiKey is not configured in production. Set ApiKey via environment variable.");
        }
        else
        {
            logger.LogWarning("SECURITY NOTICE: ApiKey is not set. API write endpoints are operating in development mode.");
        }
    }

    if (string.IsNullOrWhiteSpace(ytApiKey))
    {
        logger.LogInformation("YouTube:ApiKey is not configured. YouTube service will operate using RSS feed fallback.");
    }

    try
    {
        await initializer.InitializeAsync();
        logger.LogInformation("Database initialization completed successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Fatal error during database initialization. Application cannot start.");
        throw; // Fail fast — don't start with broken schema
    }
}

app.Run();

// Required for WebApplicationFactory<Program> in integration tests
public partial class Program { }
