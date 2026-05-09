namespace AuthService.API.Persistence;

public sealed record AdminStatsSnapshot(
    int TotalUsers,
    int ActiveUsers,
    int SuspendedUsers,
    int DeletedUsers,
    int TotalLoginEventsLast24Hours,
    int FailedLoginEventsLast24Hours,
    int FlaggedEventsLast24Hours,
    int DistinctSourceIpsLast24Hours,
    DateTime ComputedAt);

public interface IAdminStatsRepository
{
    Task<AdminStatsSnapshot?> GetCurrentAsync(CancellationToken ct = default);
    Task UpsertAsync(AdminStatsSnapshot snapshot, CancellationToken ct = default);
}
