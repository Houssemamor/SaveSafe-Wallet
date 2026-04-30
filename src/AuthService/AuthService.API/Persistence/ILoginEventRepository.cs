namespace AuthService.API.Persistence;

public sealed record LoginEventRecord(
    Guid EventId,
    Guid UserId,
    string UserEmail,
    string UserName,
    string? IpAddress,
    string? Country,
    bool Success,
    string? FailureReason,
    bool IsFlagged,
    DateTime Timestamp,
    string? UserAgent);

public interface ILoginEventRepository
{
    Task AddAsync(LoginEventRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<LoginEventRecord>> GetRecentAsync(int limit, CancellationToken ct = default);
    Task<IReadOnlyList<LoginEventRecord>> GetEventsSinceAsync(DateTime sinceUtc, CancellationToken ct = default);
}
