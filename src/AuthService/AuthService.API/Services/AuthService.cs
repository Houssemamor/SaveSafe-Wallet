using AuthService.API.Data;
using AuthService.API.DTOs;
using AuthService.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.API.Services;

public interface IAuthService
{
    Task<(AuthResponseDto response, string rawRefreshToken)> RegisterAsync(
        RegisterRequestDto request, string? ipAddress, string? userAgent);
    Task<(AuthResponseDto response, string rawRefreshToken)> LoginAsync(
        LoginRequestDto request, string? ipAddress, string? userAgent);
    Task<(AuthResponseDto response, string rawRefreshToken)> RefreshAsync(string rawRefreshToken);
    Task LogoutAsync(string rawRefreshToken);
}

public class AuthService : IAuthService
{
    private readonly AuthDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IWalletProvisioningService _walletProvisioning;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AuthDbContext db,
        ITokenService tokenService,
        IWalletProvisioningService walletProvisioning,
        ILogger<AuthService> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _walletProvisioning = walletProvisioning;
        _logger = logger;
    }

    public async Task<(AuthResponseDto response, string rawRefreshToken)> RegisterAsync(
        RegisterRequestDto request, string? ipAddress, string? userAgent)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == normalizedEmail))
            throw new InvalidOperationException("Email is already registered.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        var user = new User
        {
            Email = normalizedEmail,
            Name = request.Name,
            PasswordHash = passwordHash
        };
        _db.Users.Add(user);

        var (rawRefreshToken, refreshTokenEntity) = CreateRefreshToken(user.Id);
        _db.RefreshTokens.Add(refreshTokenEntity);

        await _db.SaveChangesAsync();

        _logger.LogInformation("User registered: {UserId} ({Email})", user.Id, user.Email);

        // Best-effort wallet provisioning - registration is not rolled back on failure
        await _walletProvisioning.CreateWalletForUserAsync(user.Id);

        return (BuildAuthResponse(user, _tokenService.GenerateAccessToken(user)), rawRefreshToken);
    }

    public async Task<(AuthResponseDto response, string rawRefreshToken)> LoginAsync(
        LoginRequestDto request, string? ipAddress, string? userAgent)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        // Always record login events - both successes and failures
        var loginEvent = new LoginEvent
        {
            UserId = user?.Id ?? Guid.Empty,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow
        };

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            loginEvent.Success = false;
            loginEvent.FailureReason = user is null ? "UserNotFound" : "InvalidPassword";

            if (user is not null)
            {
                // Only save when we have a valid user_id FK reference
                _db.LoginEvents.Add(loginEvent);
                await _db.SaveChangesAsync();
            }

            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (user.AccountStatus != UserAccountStatus.Active)
            throw new UnauthorizedAccessException("Account is not active.");

        loginEvent.Success = true;

        // Rotate: revoke all existing active refresh tokens for this user
        var existingTokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
            .ToListAsync();
        existingTokens.ForEach(rt => rt.IsRevoked = true);

        var (rawRefreshToken, refreshTokenEntity) = CreateRefreshToken(user.Id);
        _db.LoginEvents.Add(loginEvent);
        _db.RefreshTokens.Add(refreshTokenEntity);
        user.LastLoginAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return (BuildAuthResponse(user, _tokenService.GenerateAccessToken(user)), rawRefreshToken);
    }

    public async Task<(AuthResponseDto response, string rawRefreshToken)> RefreshAsync(
        string rawRefreshToken)
    {
        var hash = _tokenService.HashToken(rawRefreshToken);
        var tokenRecord = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt =>
                rt.TokenHash == hash &&
                !rt.IsRevoked &&
                rt.ExpiresAt > DateTime.UtcNow);

        if (tokenRecord is null)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        // Token rotation: old token is revoked, new token issued
        tokenRecord.IsRevoked = true;
        var (newRawToken, newTokenEntity) = CreateRefreshToken(tokenRecord.UserId);
        _db.RefreshTokens.Add(newTokenEntity);
        await _db.SaveChangesAsync();

        return (BuildAuthResponse(tokenRecord.User,
            _tokenService.GenerateAccessToken(tokenRecord.User)), newRawToken);
    }

    public async Task LogoutAsync(string rawRefreshToken)
    {
        var hash = _tokenService.HashToken(rawRefreshToken);
        var tokenRecord = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == hash && !rt.IsRevoked);

        if (tokenRecord is not null)
        {
            tokenRecord.IsRevoked = true;
            await _db.SaveChangesAsync();
        }
    }

    private (string raw, RefreshToken entity) CreateRefreshToken(Guid userId)
    {
        var raw = _tokenService.GenerateRefreshToken();
        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = _tokenService.HashToken(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        return (raw, entity);
    }

    private static AuthResponseDto BuildAuthResponse(User user, string accessToken) =>
        new(
            AccessToken: accessToken,
            TokenType: "Bearer",
            ExpiresIn: 900,
            UserId: user.Id,
            Email: user.Email,
            Name: user.Name,
            Role: user.Role.ToString()
        );
}
