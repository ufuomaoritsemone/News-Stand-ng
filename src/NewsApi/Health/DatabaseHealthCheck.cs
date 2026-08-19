using Microsoft.Extensions.Diagnostics.HealthChecks;
using NewsApi.Data;

namespace NewsApi.Health;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly NewsDbContext _db;

    public DatabaseHealthCheck(NewsDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                return HealthCheckResult.Healthy("Database connection is healthy.");
            }

            return HealthCheckResult.Unhealthy("Database connection failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check failed with exception.", ex);
        }
    }
}
