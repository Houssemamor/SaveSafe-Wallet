using AuthService.API.Attributes;
using AuthService.API.DTOs;
using AuthService.API.Entities;
using AuthService.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize]
[RequireRole(UserRole.Admin)]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("security-summary")]
    [ProducesResponseType(typeof(AdminSecuritySummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSecuritySummary()
    {
        var summary = await _adminService.GetSecuritySummaryAsync();
        return Ok(summary);
    }

    [HttpPost("security-summary/refresh")]
    [ProducesResponseType(typeof(AdminSecuritySummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshSecuritySummary()
    {
        var summary = await _adminService.RefreshSecuritySummaryAsync();
        return Ok(summary);
    }

    [HttpGet("login-events")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminLoginEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLoginEvents([FromQuery] int limit = 50)
    {
        var events = await _adminService.GetLoginEventsAsync(limit);
        return Ok(events);
    }

    [HttpGet("failed-logins")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminFailedLoginByIpDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFailedLogins([FromQuery] int top = 20)
    {
        var failedLogins = await _adminService.GetFailedLoginsByIpAsync(top);
        return Ok(failedLogins);
    }

    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] int limit = 100)
    {
        var users = await _adminService.GetUsersAsync(limit);
        return Ok(users);
    }

    /// <summary>Suspend a user account.</summary>
    [HttpPost("users/{userId}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendUser(Guid userId)
    {
        await _adminService.SuspendUserAsync(userId);
        return NoContent();
    }

    /// <summary>Activate a suspended user account.</summary>
    [HttpPost("users/{userId}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateUser(Guid userId)
    {
        await _adminService.ActivateUserAsync(userId);
        return NoContent();
    }

    /// <summary>Delete a user account (soft delete).</summary>
    [HttpDelete("users/{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        await _adminService.DeleteUserAsync(userId);
        return NoContent();
    }

    /// <summary>Debug endpoint to check user count and database status.</summary>
    [HttpGet("debug/user-count")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserCountDebug()
    {
        var users = await _adminService.GetUsersAsync(1000);
        return Ok(new
        {
            TotalCount = users.Count,
            Users = users.Select(u => new
            {
                u.UserId,
                u.Email,
                u.Name,
                u.Role,
                u.AccountStatus,
                u.MfaEnabled,
                u.CreatedAt,
                u.LastLoginAt
            }).ToList()
        });
    }
}
