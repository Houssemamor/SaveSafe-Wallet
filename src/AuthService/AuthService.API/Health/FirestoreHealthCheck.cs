using AuthService.API.Persistence.Firestore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AuthService.API.Health;

public sealed class FirestoreHealthCheck : IHealthCheck
{
    private readonly IFirestoreDbProvider _dbProvider;

    public FirestoreHealthCheck(IFirestoreDbProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Read-only probe to verify credentials and connectivity.
            var db = _dbProvider.GetDb();
            await db.Collection("healthcheck").Limit(1).GetSnapshotAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Firestore health check failed.", ex);
        }
    }
}
