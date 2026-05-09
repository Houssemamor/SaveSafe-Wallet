namespace AuthService.API.Persistence;

public sealed record RefreshTokenRecord(
    Guid UserId,
    string TokenHash,
    DateTime ExpiresAt,
    bool IsRevoked,
    DateTime CreatedAt);

public interface IRefreshTokenRepository
{
    Task CreateAsync(RefreshTokenRecord token, CancellationToken ct = default);
    Task<Guid> RotateAsync(string currentTokenHash, RefreshTokenRecord newToken, CancellationToken ct = default);
    Task RevokeAsync(string tokenHash, CancellationToken ct = default);
    Task RevokeAllActiveForUserAsync(Guid userId, CancellationToken ct = default);
}
