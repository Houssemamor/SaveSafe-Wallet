using AuthService.API.Entities;

namespace AuthService.API.Persistence;

public sealed record UserCounts(
    int TotalUsers,
    int ActiveUsers,
    int SuspendedUsers,
    int DeletedUsers);

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task CreateAsync(User user, string normalizedEmail, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task UpdateLastLoginAsync(Guid userId, DateTime lastLoginAt, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetRecentUsersAsync(int limit, CancellationToken ct = default);
    Task<UserCounts> GetUserCountsAsync(CancellationToken ct = default);
    Task<bool> AnyWithRoleAsync(UserRole role, CancellationToken ct = default);
}
