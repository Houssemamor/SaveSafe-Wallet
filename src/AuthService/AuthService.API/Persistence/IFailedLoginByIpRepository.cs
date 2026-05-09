namespace AuthService.API.Persistence;

public sealed record FailedLoginByIpRecord(
    string IpAddress,
    int FailedAttempts,
    DateTime LastAttemptAt);

public interface IFailedLoginByIpRepository
{
    Task IncrementAsync(string ipAddress, DateTime timestamp, CancellationToken ct = default);
    Task<IReadOnlyList<FailedLoginByIpRecord>> GetTopAsync(int top, CancellationToken ct = default);
}
