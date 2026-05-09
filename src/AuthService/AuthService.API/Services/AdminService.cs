using AuthService.API.DTOs;
using AuthService.API.Entities;
using AuthService.API.Persistence;
using Microsoft.Extensions.Logging;

namespace AuthService.API.Services;

public interface IAdminService
{
    Task<AdminSecuritySummaryDto> GetSecuritySummaryAsync();
    Task<AdminSecuritySummaryDto> RefreshSecuritySummaryAsync();
    Task<IReadOnlyList<AdminLoginEventDto>> GetLoginEventsAsync(int limit);
    Task<IReadOnlyList<AdminFailedLoginByIpDto>> GetFailedLoginsByIpAsync(int top);
    Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(int limit);
    Task SuspendUserAsync(Guid userId);
    Task ActivateUserAsync(Guid userId);
    Task DeleteUserAsync(Guid userId);
}

public class AdminService : IAdminService
{
    private readonly IUserRepository _users;
    private readonly ILoginEventRepository _loginEvents;
    private readonly IFailedLoginByIpRepository _failedLoginsByIp;
    private readonly IAdminStatsRepository _adminStats;
    private readonly IAdminStatsRefresher _adminStatsRefresher;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IUserRepository users,
        ILoginEventRepository loginEvents,
        IFailedLoginByIpRepository failedLoginsByIp,
        IAdminStatsRepository adminStats,
        IAdminStatsRefresher adminStatsRefresher,
        ILogger<AdminService> logger)
    {
        _users = users;
        _loginEvents = loginEvents;
        _failedLoginsByIp = failedLoginsByIp;
        _adminStats = adminStats;
        _adminStatsRefresher = adminStatsRefresher;
        _logger = logger;
    }

    public async Task<AdminSecuritySummaryDto> GetSecuritySummaryAsync()
    {
        var snapshot = await _adminStats.GetCurrentAsync();
        if (snapshot is null)
        {
            snapshot = await _adminStatsRefresher.RefreshAsync();
        }

        return MapSnapshot(snapshot);
    }

    public async Task<AdminSecuritySummaryDto> RefreshSecuritySummaryAsync()
    {
        var snapshot = await _adminStatsRefresher.RefreshAsync();
        return MapSnapshot(snapshot);
    }

    public async Task<IReadOnlyList<AdminLoginEventDto>> GetLoginEventsAsync(int limit)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);

        var events = await _loginEvents.GetRecentAsync(safeLimit);
        return events
            .Select(item => new AdminLoginEventDto(
                item.EventId,
                item.UserId,
                item.UserEmail,
                item.UserName,
                item.IpAddress,
                item.Country,
                item.Success,
                item.FailureReason,
                item.IsFlagged,
                item.Timestamp))
            .ToList();
    }

    public async Task<IReadOnlyList<AdminFailedLoginByIpDto>> GetFailedLoginsByIpAsync(int top)
    {
        var safeTop = Math.Clamp(top, 1, 100);

        var items = await _failedLoginsByIp.GetTopAsync(safeTop);
        return items
            .Select(item => new AdminFailedLoginByIpDto(
                item.IpAddress,
                item.FailedAttempts,
                item.LastAttemptAt))
            .ToList();
    }

    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(int limit)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);

        _logger.LogInformation("Fetching recent users with limit: {Limit}", safeLimit);

        var users = await _users.GetRecentUsersAsync(safeLimit);

        _logger.LogInformation("Retrieved {Count} users from database", users.Count);

        return users
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email,
                u.Name,
                u.Role.ToString(),
                u.AccountStatus.ToString(),
                u.MfaEnabled,
                u.CreatedAt,
                u.LastLoginAt))
            .ToList();
    }

    public async Task SuspendUserAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new KeyNotFoundException($"User {userId} not found.");

        if (user.AccountStatus == UserAccountStatus.Suspended)
            return; // Already suspended

        user.AccountStatus = UserAccountStatus.Suspended;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
    }

    public async Task ActivateUserAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new KeyNotFoundException($"User {userId} not found.");

        if (user.AccountStatus == UserAccountStatus.Active)
            return; // Already active

        user.AccountStatus = UserAccountStatus.Active;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new KeyNotFoundException($"User {userId} not found.");

        if (user.AccountStatus == UserAccountStatus.Deleted)
            return; // Already deleted

        user.AccountStatus = UserAccountStatus.Deleted;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
    }

    private static AdminSecuritySummaryDto MapSnapshot(AdminStatsSnapshot snapshot) =>
        new(
            TotalUsers: snapshot.TotalUsers,
            ActiveUsers: snapshot.ActiveUsers,
            SuspendedUsers: snapshot.SuspendedUsers,
            DeletedUsers: snapshot.DeletedUsers,
            TotalLoginEventsLast24Hours: snapshot.TotalLoginEventsLast24Hours,
            FailedLoginEventsLast24Hours: snapshot.FailedLoginEventsLast24Hours,
            FlaggedEventsLast24Hours: snapshot.FlaggedEventsLast24Hours,
            DistinctSourceIpsLast24Hours: snapshot.DistinctSourceIpsLast24Hours);
}
