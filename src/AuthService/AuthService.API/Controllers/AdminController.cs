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

    [HttpPost("observability/loki/query")]
    [ProducesResponseType(typeof(AdminLokiQueryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> QueryLoki([FromBody] AdminLokiQueryRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await _adminService.QueryLokiAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "Loki query timed out.");
        }
    }

    [HttpGet("ai/review-queue")]
    [ProducesResponseType(typeof(AdminAiReviewQueueResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetAiReviewQueue([FromQuery] int limit = 25, CancellationToken ct = default)
    {
        try
        {
            var queue = await _adminService.GetAiReviewQueueAsync(limit, ct);
            return Ok(queue);
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "AI security service timed out.");
        }
    }

    [HttpPost("ai/review-queue/{eventId}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ResolveAiReviewItem(string eventId, CancellationToken ct)
    {
        try
        {
            await _adminService.ResolveAiReviewItemAsync(eventId, ct);
            return NoContent();
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, "AI security service timed out.");
        }
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

    [HttpPut("users/{userId}/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetUserPassword(Guid userId, [FromBody] AdminResetUserPasswordRequestDto request)
    {
        try
        {
            await _adminService.ResetUserPasswordAsync(userId, request);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
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
