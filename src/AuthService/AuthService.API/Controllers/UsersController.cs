using AuthService.API.DTOs;
using AuthService.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;

namespace AuthService.API.Controllers;

/// <summary>
/// User profile CRUD endpoints.
/// Separated from AuthController because the route prefix differs: /api/users vs /api/auth.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public UsersController(IAuthService authService, IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _authService = authService;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    /// <summary>Get authenticated user's profile information.</summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var profile = await _authService.GetUserProfileAsync(Guid.Parse(userId));
        return Ok(profile);
    }

    /// <summary>Update authenticated user's profile.</summary>
    [HttpPut("profile")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        await _authService.UpdateUserProfileAsync(Guid.Parse(userId), request);
        return NoContent();
    }

    /// <summary>
    /// Proxy and cache external profile avatar images.
    /// Accepts a public image URL as a query parameter and returns cached bytes.
    /// This avoids clients making repeated direct requests to Googleusercontent which can hit rate limits (429).
    /// </summary>
    [AllowAnonymous]
    [HttpGet("profile/avatar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfileAvatar([FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("Missing 'url' query parameter");

        try
        {
            // Validate it's a valid URL to prevent open redirect
            var parsedUrl = new Uri(url);
            if (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps)
                return BadRequest("URL must use http or https");
        }
        catch
        {
            return BadRequest("Invalid URL format");
        }

        var cacheKey = $"avatar:{url}";
        if (_cache.TryGetValue(cacheKey, out byte[] cachedBytes) && cachedBytes?.Length > 0)
        {
            return File(cachedBytes, "image/jpeg");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
                return StatusCode((int)resp.StatusCode);

            var bytes = await resp.Content.ReadAsByteArrayAsync();
            var contentType = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";

            // Cache for 24 hours
            _cache.Set(cacheKey, bytes, TimeSpan.FromHours(24));

            return File(bytes, contentType);
        }
        catch (TaskCanceledException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout);
        }
        catch
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>Soft-delete user's account. Revokes all tokens and clears refresh cookie.</summary>
    [HttpDelete("profile")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        await _authService.DeleteUserAccountAsync(Guid.Parse(userId));
        Response.Cookies.Delete("ssw_refresh", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict
        });
        return NoContent();
    }
}
