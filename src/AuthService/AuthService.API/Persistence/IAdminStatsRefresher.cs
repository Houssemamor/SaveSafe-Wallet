namespace AuthService.API.Persistence;

public interface IAdminStatsRefresher
{
    Task<AdminStatsSnapshot> RefreshAsync(CancellationToken ct = default);
}
