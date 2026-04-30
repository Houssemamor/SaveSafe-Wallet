using AuthService.API.DTOs;
using AuthService.API.Entities;
using AuthService.API.Persistence;

namespace AuthService.API.Services;

public interface IAuthService
{
    Task<(AuthResponseDto response, string rawRefreshToken)> RegisterAsync(
        RegisterRequestDto request, string? ipAddress, string? userAgent);
    Task<(AuthResponseDto response, string rawRefreshToken)> LoginAsync(
        LoginRequestDto request, string? ipAddress, string? userAgent);
    Task<(AuthResponseDto response, string rawRefreshToken)> GoogleLoginAsync(
        string idToken, string? ipAddress, string? userAgent);
    Task<(AuthResponseDto response, string rawRefreshToken)> RefreshAsync(string rawRefreshToken);
    Task LogoutAsync(string rawRefreshToken);
    Task<UserProfileDto> GetUserProfileAsync(Guid userId);
    Task UpdateUserProfileAsync(Guid userId, UpdateProfileRequestDto request);
    Task DeleteUserAccountAsync(Guid userId);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ILoginEventRepository _loginEvents;
    private readonly IFailedLoginByIpRepository _failedLoginByIp;
    private readonly IAuthRegistrationStore _registrationStore;
    private readonly ITokenService _tokenService;
    private readonly IWalletProvisioningService _walletProvisioning;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        ILoginEventRepository loginEvents,
        IFailedLoginByIpRepository failedLoginByIp,
        IAuthRegistrationStore registrationStore,
        ITokenService tokenService,
        IWalletProvisioningService walletProvisioning,
        ILogger<AuthService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _loginEvents = loginEvents;
        _failedLoginByIp = failedLoginByIp;
        _registrationStore = registrationStore;
        _tokenService = tokenService;
        _walletProvisioning = walletProvisioning;
        _logger = logger;
    }

    public async Task<(AuthResponseDto response, string rawRefreshToken)> RegisterAsync(
        RegisterRequestDto request, string? ipAddress, string? userAgent)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await _users.EmailExistsAsync(normalizedEmail))
            throw new InvalidOperationException("Email is already registered.");

        var now = DateTime.UtcNow;
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);

        var user = new User
        {
            Email = normalizedEmail,
            Name = request.Name,
            PasswordHash = passwordHash,
            CreatedAt = now,
            UpdatedAt = now
        };

        var (rawRefreshToken, refreshTokenRecord) = CreateRefreshToken(user.Id, now);

        try
        {
            // Single transaction: user, email index, and refresh token must be created together.
            await _registrationStore.RegisterAsync(user, normalizedEmail, refreshTokenRecord);
        }
        catch (InvalidOperationException)
        {
            throw;
        }

        _logger.LogInformation("User registered: {UserId} ({Email})", user.Id, user.Email);

        // Best-effort wallet provisioning - registration is not rolled back on failure.
        await _walletProvisioning.CreateWalletForUserAsync(user.Id);

        return (BuildAuthResponse(user, _tokenService.GenerateAccessToken(user)), rawRefreshToken);
    }

    public async Task<(AuthResponseDto response, string rawRefreshToken)> LoginAsync(
        LoginRequestDto request, string? ipAddress, string? userAgent)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(normalizedEmail);
        var now = DateTime.UtcNow;

        var loginEvent = user is null
            ? null
            : new LoginEventRecord(
                EventId: Guid.NewGuid(),
                UserId: user.Id,
                UserEmail: user.Email,
                UserName: user.Name,
                IpAddress: ipAddress,
                Country: null,
                Success: false,
                FailureReason: null,
                IsFlagged: false,
                Timestamp: now,
                UserAgent: userAgent);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            if (loginEvent is not null)
            {
                loginEvent = loginEvent with
                {
                    Success = false,
                    FailureReason = user is null ? "UserNotFound" : "InvalidPassword"
                };

                await _loginEvents.AddAsync(loginEvent);
                await _failedLoginByIp.IncrementAsync(
                    NormalizeIp(ipAddress), now);
            }

            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (user.AccountStatus != UserAccountStatus.Active)
            throw new UnauthorizedAccessException("Account is not active.");

        // Rotate: revoke all existing active refresh tokens for this user.
        await _refreshTokens.RevokeAllActiveForUserAsync(user.Id);

        var (rawRefreshToken, refreshTokenRecord) = CreateRefreshToken(user.Id, now);
        await _refreshTokens.CreateAsync(refreshTokenRecord);

        if (loginEvent is not null)
        {
            loginEvent = loginEvent with { Success = true };
            await _loginEvents.AddAsync(loginEvent);
        }

        await _users.UpdateLastLoginAsync(user.Id, now);

        return (BuildAuthResponse(user, _tokenService.GenerateAccessToken(user)), rawRefreshToken);
    }

    public async Task<(AuthResponseDto response, string rawRefreshToken)> GoogleLoginAsync(
        string idToken, string? ipAddress, string? userAgent)
    {
        try
        {
            // Verify the Google ID token
            var payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(idToken);

            if (payload == null)
                throw new UnauthorizedAccessException("Invalid Google token.");

            var normalizedEmail = payload.Email.Trim().ToLowerInvariant();
            var user = await _users.GetByEmailAsync(normalizedEmail);
            var now = DateTime.UtcNow;

            // Create login event for tracking
            var loginEvent = new LoginEventRecord(
                EventId: Guid.NewGuid(),
                UserId: user?.Id ?? Guid.Empty,
                UserEmail: normalizedEmail,
                UserName: payload.Name,
                IpAddress: ipAddress,
                Country: null,
                Success: false,
                FailureReason: null,
                IsFlagged: false,
                Timestamp: now,
                UserAgent: userAgent);

            if (user is null)
            {
                // Create new user from Google account
                user = new User
                {
                    Email = normalizedEmail,
                    Name = payload.Name,
                    PasswordHash = string.Empty, // No password for Google accounts
                    CreatedAt = now,
                    UpdatedAt = now,
                    AccountStatus = UserAccountStatus.Active,
                    Role = UserRole.User
                };

                var (rawRefreshToken, refreshTokenRecord) = CreateRefreshToken(user.Id, now);

                try
                {
                    await _registrationStore.RegisterAsync(user, normalizedEmail, refreshTokenRecord);
                }
                catch (InvalidOperationException)
                {
                    throw;
                }

                _logger.LogInformation("User registered via Google: {UserId} ({Email})", user.Id, user.Email);

                // Best-effort wallet provisioning
                await _walletProvisioning.CreateWalletForUserAsync(user.Id);

                loginEvent = loginEvent with { Success = true, UserId = user.Id };
                await _loginEvents.AddAsync(loginEvent);

                return (BuildAuthResponse(user, _tokenService.GenerateAccessToken(user)), rawRefreshToken);
            }
            else
            {
                // Existing user - log them in
                if (user.AccountStatus != UserAccountStatus.Active)
                    throw new UnauthorizedAccessException("Account is not active.");

                // Rotate: revoke all existing active refresh tokens for this user.
                await _refreshTokens.RevokeAllActiveForUserAsync(user.Id);

                var (rawRefreshToken, refreshTokenRecord) = CreateRefreshToken(user.Id, now);
                await _refreshTokens.CreateAsync(refreshTokenRecord);

                loginEvent = loginEvent with { Success = true, UserId = user.Id };
                await _loginEvents.AddAsync(loginEvent);

                await _users.UpdateLastLoginAsync(user.Id, now);

                return (BuildAuthResponse(user, _tokenService.GenerateAccessToken(user)), rawRefreshToken);
            }
        }
        catch (Google.Apis.Auth.InvalidJwtException)
        {
            throw new UnauthorizedAccessException("Invalid Google token.");
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Google login failed");
            throw new UnauthorizedAccessException("Google login failed.");
        }
    }

    public async Task<(AuthResponseDto response, string rawRefreshToken)> RefreshAsync(
        string rawRefreshToken)
    {
        var hash = _tokenService.HashToken(rawRefreshToken);
        var now = DateTime.UtcNow;
        var (newRawToken, newTokenRecord) = CreateRefreshToken(Guid.Empty, now);

        var userId = await _refreshTokens.RotateAsync(hash, newTokenRecord);
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        return (BuildAuthResponse(user, _tokenService.GenerateAccessToken(user)), newRawToken);
    }

    public async Task LogoutAsync(string rawRefreshToken)
    {
        var hash = _tokenService.HashToken(rawRefreshToken);
        await _refreshTokens.RevokeAsync(hash);
    }

    /// <summary>Retrieve authenticated user's profile information. Used by GET /api/users/profile.</summary>
    public async Task<UserProfileDto> GetUserProfileAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);

        if (user is null)
            throw new KeyNotFoundException($"User {userId} not found.");

        return new UserProfileDto(
            UserId: user.Id,
            Email: user.Email,
            Name: user.Name,
            MfaEnabled: user.MfaEnabled,
            AccountStatus: user.AccountStatus.ToString(),
            Role: user.Role.ToString(),
            CreatedAt: user.CreatedAt,
            LastLoginAt: user.LastLoginAt
        );
    }

    /// <summary>Update authenticated user's profile. Used by PUT /api/users/profile.</summary>
    public async Task UpdateUserProfileAsync(Guid userId, UpdateProfileRequestDto request)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new KeyNotFoundException($"User {userId} not found.");

        user.Name = request.Name;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.UpdateAsync(user);

        _logger.LogInformation("User {UserId} profile updated", userId);
    }

    /// <summary>Delete user account (soft-delete: mark as Deleted). Used by DELETE /api/users/{id}.</summary>
    public async Task DeleteUserAccountAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new KeyNotFoundException($"User {userId} not found.");

        // Mark account as deleted instead of hard-delete (preserves audit trail)
        user.AccountStatus = UserAccountStatus.Deleted;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.UpdateAsync(user);

        // Revoke all active refresh tokens.
        await _refreshTokens.RevokeAllActiveForUserAsync(userId);

        _logger.LogInformation("User {UserId} account deleted (soft)", userId);
    }

    private (string raw, RefreshTokenRecord record) CreateRefreshToken(Guid userId, DateTime now)
    {
        var raw = _tokenService.GenerateRefreshToken();
        var record = new RefreshTokenRecord(
            UserId: userId,
            TokenHash: _tokenService.HashToken(raw),
            ExpiresAt: now.AddDays(7),
            IsRevoked: false,
            CreatedAt: now);
        return (raw, record);
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

    private static string NormalizeIp(string? ipAddress) =>
        string.IsNullOrWhiteSpace(ipAddress)
            ? "unknown"
            : ipAddress.Replace("/", "_");
}
