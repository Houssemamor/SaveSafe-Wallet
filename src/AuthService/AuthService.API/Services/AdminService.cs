using AuthService.API.Data;
using AuthService.API.DTOs;
using AuthService.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.API.Services;

public interface IAdminService
{
    Task<AdminSecuritySummaryDto> GetSecuritySummaryAsync();
    Task<IReadOnlyList<AdminLoginEventDto>> GetLoginEventsAsync(int limit);
    Task<IReadOnlyList<AdminFailedLoginByIpDto>> GetFailedLoginsByIpAsync(int top);
    Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(int limit);
}

public class AdminService : IAdminService
{
    private readonly AuthDbContext _db;

    public AdminService(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<AdminSecuritySummaryDto> GetSecuritySummaryAsync()
    {
        var last24Hours = DateTime.UtcNow.AddHours(-24);

        // AuthDbContext does not support concurrent operations; keep the queries sequential.
        var totalUsers = await _db.Users.CountAsync();
        var activeUsers = await _db.Users.CountAsync(u => u.AccountStatus == UserAccountStatus.Active);
        var suspendedUsers = await _db.Users.CountAsync(u => u.AccountStatus == UserAccountStatus.Suspended);
        var deletedUsers = await _db.Users.CountAsync(u => u.AccountStatus == UserAccountStatus.Deleted);

        var eventsLast24HoursQuery = _db.LoginEvents.Where(e => e.Timestamp >= last24Hours);
        var totalEvents = await eventsLast24HoursQuery.CountAsync();
        var failedEvents = await eventsLast24HoursQuery.CountAsync(e => !e.Success);
        var flaggedEvents = await eventsLast24HoursQuery.CountAsync(e => e.IsFlagged);
        var distinctIps = await eventsLast24HoursQuery
            .Where(e => e.IpAddress != null)
            .Select(e => e.IpAddress!)
            .Distinct()
            .CountAsync();

        return new AdminSecuritySummaryDto(
            TotalUsers: totalUsers,
            ActiveUsers: activeUsers,
            SuspendedUsers: suspendedUsers,
            DeletedUsers: deletedUsers,
            TotalLoginEventsLast24Hours: totalEvents,
            FailedLoginEventsLast24Hours: failedEvents,
            FlaggedEventsLast24Hours: flaggedEvents,
            DistinctSourceIpsLast24Hours: distinctIps);
    }

    public async Task<IReadOnlyList<AdminLoginEventDto>> GetLoginEventsAsync(int limit)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);

        return await _db.LoginEvents
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .Take(safeLimit)
            .Join(
                _db.Users.AsNoTracking(),
                eventItem => eventItem.UserId,
                user => user.Id,
                (eventItem, user) => new AdminLoginEventDto(
                    eventItem.Id,
                    eventItem.UserId,
                    user.Email,
                    user.Name,
                    eventItem.IpAddress,
                    eventItem.Country,
                    eventItem.Success,
                    eventItem.FailureReason,
                    eventItem.IsFlagged,
                    eventItem.Timestamp))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AdminFailedLoginByIpDto>> GetFailedLoginsByIpAsync(int top)
    {
        var safeTop = Math.Clamp(top, 1, 100);

        var groupedResults = await _db.LoginEvents
            .AsNoTracking()
            .Where(e => !e.Success)
            .GroupBy(e => e.IpAddress ?? "unknown")
            .Select(group => new
            {
                IpAddress = group.Key,
                FailedAttempts = group.Count(),
                LastAttemptAt = group.Max(e => e.Timestamp)
            })
            .OrderByDescending(item => item.FailedAttempts)
            .ThenByDescending(item => item.LastAttemptAt)
            .Take(safeTop)
            .ToListAsync();

        return groupedResults
            .Select(item => new AdminFailedLoginByIpDto(
                item.IpAddress,
                item.FailedAttempts,
                item.LastAttemptAt))
            .ToList();
    }

    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(int limit)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);

        return await _db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Take(safeLimit)
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email,
                u.Name,
                u.Role.ToString(),
                u.AccountStatus.ToString(),
                u.MfaEnabled,
                u.CreatedAt,
                u.LastLoginAt))
            .ToListAsync();
    }
}
