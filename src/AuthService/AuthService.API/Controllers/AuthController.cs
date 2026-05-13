using AuthService.API.Attributes;
using AuthService.API.DTOs;
using AuthService.API.Services;
using AuthService.API.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuthService.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly IUserRepository _userRepository;
    private readonly IMfaService _mfaService;

    public AuthController(IAuthService authService, IUserService userService, IUserRepository userRepository, IMfaService mfaService)
    {
        _authService = authService;
        _userService = userService;
        _userRepository = userRepository;
        _mfaService = mfaService;
    }

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
        if (!string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            SetRefreshTokenCookie(rawRefreshToken);
        }
        return Ok(response);
    }

    [HttpGet("mfa/questions")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetMfaQuestions()
    {
        return Ok(new { questions = _authService.GetMfaQuestionCatalog() });
    }

    [HttpPost("mfa/enroll")]
    [Authorize]
    [ProducesResponseType(typeof(MfaEnrollResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnrollMfa([FromBody] MfaEnrollRequestDto request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User not authenticated.");

        var response = await _authService.EnrollMfaAsync(Guid.Parse(userId), request);
        return Ok(response);
    }

    [HttpPost("mfa/disable")]
    [Authorize]
    [ProducesResponseType(typeof(MfaDisableResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableMfa()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User not authenticated.");

        var response = await _authService.DisableMfaAsync(Guid.Parse(userId));
        return Ok(response);
    }

    [HttpPost("mfa/verify")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyRequestDto request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var ua = HttpContext.Request.Headers.UserAgent.ToString();

        var (response, rawRefreshToken) = await _authService.VerifyMfaLoginAsync(request, ip, ua);
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
        if (!string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            SetRefreshTokenCookie(rawRefreshToken);
        }
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

    /// <summary>Internal API: Look up user by email for service-to-service communication.</summary>
    [HttpGet("internal/user/by-email/{email}")]
    [InternalApi]
    [ProducesResponseType(typeof(InternalUserLookupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserByEmailInternal(string email)
    {
        var userId = await _userService.GetUserIdByEmailAsync(email);
        if (userId is null)
        {
            return NotFound(new InternalUserLookupDto(null, null, null));
        }

        var userName = await _userService.GetUserNameAsync(userId.Value);
        return Ok(new InternalUserLookupDto(userId, userName, email));
    }

    /// <summary>Internal API: Look up user profile by ID for service-to-service communication.</summary>
    [HttpGet("internal/user/{id}")]
    [InternalApi]
    [ProducesResponseType(typeof(InternalUserLookupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserByIdInternal(string id)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return BadRequest("Invalid user id");
        }

        var userName = await _userService.GetUserNameAsync(userId);
        if (userName is null)
        {
            return NotFound(new InternalUserLookupDto(null, null, null));
        }

        return Ok(new InternalUserLookupDto(userId, userName, null));
    }

    [HttpPost("mfa/forgot/initiate")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ForgotInitiateResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotInitiate([FromBody] ForgotInitiateRequestDto request)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new ForgotInitiateResponseDto(false, null, null));

        var userId = await _userService.GetUserIdByEmailAsync(email);
        if (userId is null)
            return NotFound(new ForgotInitiateResponseDto(false, null, null));

        var user = await _userRepository.GetByIdAsync(userId.Value);
        if (user is null || !user.MfaEnabled)
            return BadRequest(new ForgotInitiateResponseDto(false, null, null));

        var challenge = await _mfaService.CreateChallengeAsync(userId.Value);
        return Ok(new ForgotInitiateResponseDto(true, challenge.QuestionText, challenge.ChallengeToken));
    }

    [HttpPost("mfa/forgot/verify")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ForgotVerifyResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotVerify([FromBody] ForgotVerifyRequestDto request)
    {
        try
        {
            // Verify the challenge and get the user id
            var id = await _mfaService.VerifyChallengeAsync(request.ChallengeToken, request.Answer);
            var signingKey = HttpContext.RequestServices.GetService(typeof(IConfiguration)) is IConfiguration cfg
                ? cfg["Jwt:Key"] ?? string.Empty
                : string.Empty;

            var resetToken = PasswordResetTokenCodec.Create(id, signingKey);
            return Ok(new ForgotVerifyResponseDto(true, resetToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new ForgotVerifyResponseDto(false, null));
        }
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResetPasswordResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        var signingKey = HttpContext.RequestServices.GetService(typeof(IConfiguration)) is IConfiguration cfg
            ? cfg["Jwt:Key"] ?? string.Empty
            : string.Empty;

        if (!PasswordResetTokenCodec.TryValidate(request.PasswordResetToken, signingKey, out var userId, out var err))
        {
            return Unauthorized(new ResetPasswordResponseDto(false));
        }

        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
            return NotFound(new ResetPasswordResponseDto(false));

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);

        return Ok(new ResetPasswordResponseDto(true));
    }
}
