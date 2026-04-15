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
}
