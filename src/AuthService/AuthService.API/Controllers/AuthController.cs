using AuthService.API.DTOs;
using AuthService.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    /// <summary>Register a new user account. Auto-creates a wallet.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = HttpContext.Request.Headers.UserAgent.ToString();

        var (response, rawRefreshToken) = await _authService.RegisterAsync(request, ip, ua);
        SetRefreshTokenCookie(rawRefreshToken);
        return CreatedAtAction(nameof(Register), response);
    }

    /// <summary>Authenticate with email and password. Returns JWT access token.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = HttpContext.Request.Headers.UserAgent.ToString();

        var (response, rawRefreshToken) = await _authService.LoginAsync(request, ip, ua);
        SetRefreshTokenCookie(rawRefreshToken);
        return Ok(response);
    }

    /// <summary>Authenticate with Google OAuth. Returns JWT access token.</summary>
    [HttpPost("google-login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequestDto request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = HttpContext.Request.Headers.UserAgent.ToString();

        var (response, rawRefreshToken) = await _authService.GoogleLoginAsync(request.IdToken, ip, ua);
        SetRefreshTokenCookie(rawRefreshToken);
        return Ok(response);
    }

    /// <summary>Use the refresh token cookie to get a new access token.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh()
    {
        var rawToken = Request.Cookies["ssw_refresh"];
        if (string.IsNullOrWhiteSpace(rawToken))
            return Unauthorized(new { message = "No refresh token provided." });

        var (response, newRawToken) = await _authService.RefreshAsync(rawToken);
        SetRefreshTokenCookie(newRawToken);
        return Ok(response);
    }

    /// <summary>Invalidate the refresh token and clear the cookie.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        var rawToken = Request.Cookies["ssw_refresh"];
        if (!string.IsNullOrWhiteSpace(rawToken))
            await _authService.LogoutAsync(rawToken);

        Response.Cookies.Delete("ssw_refresh", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict
        });
        return NoContent();
    }

    private void SetRefreshTokenCookie(string rawToken)
    {
        Response.Cookies.Append("ssw_refresh", rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}
