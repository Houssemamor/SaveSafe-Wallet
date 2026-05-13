using AuthService.API.DTOs;
using AuthService.API.Entities;
using AuthService.API.Persistence;
using FirebaseAdmin.Auth;
using KafkaInfrastructure;

namespace AuthService.API.Services;

public interface IAuthService
{
    Task<(AuthResponseDto response, string rawRefreshToken)> RegisterAsync(
        RegisterRequestDto request, string? ipAddress, string? userAgent);
    Task<(AuthResponseDto response, string? rawRefreshToken)> LoginAsync(
        LoginRequestDto request, string? ipAddress, string? userAgent);
    Task<(AuthResponseDto response, string? rawRefreshToken)> GoogleLoginAsync(
        string idToken, string? ipAddress, string? userAgent);
    Task<(AuthResponseDto response, string rawRefreshToken)> RefreshAsync(string rawRefreshToken);
    Task LogoutAsync(string rawRefreshToken);
    Task<UserProfileDto> GetUserProfileAsync(Guid userId);
    Task UpdateUserProfileAsync(Guid userId, UpdateProfileRequestDto request);
    Task UpdatePasswordAsync(Guid userId, UpdatePasswordRequestDto request);
    Task DeleteUserAccountAsync(Guid userId);
    IReadOnlyList<SecurityQuestionCatalogDto> GetMfaQuestionCatalog();
    Task<MfaEnrollResponseDto> EnrollMfaAsync(Guid userId, MfaEnrollRequestDto request);
    Task<MfaDisableResponseDto> DisableMfaAsync(Guid userId);
    Task<(AuthResponseDto response, string rawRefreshToken)> VerifyMfaLoginAsync(
        MfaVerifyRequestDto request, string? ipAddress, string? userAgent);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ILoginEventRepository _loginEvents;
    private readonly IFailedLoginByIpRepository _failedLoginByIp;
    private readonly IAuthRegistrationStore _registrationStore;
    private readonly ITokenService _tokenService;
    private readonly IMfaService _mfaService;
    private readonly IWalletProvisioningService _walletProvisioning;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly FirebaseAuth _firebaseAuth;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        ILoginEventRepository loginEvents,
        IFailedLoginByIpRepository failedLoginByIp,
        IAuthRegistrationStore registrationStore,
        ITokenService tokenService,
        IMfaService mfaService,
        IWalletProvisioningService walletProvisioning,
        IKafkaProducer kafkaProducer,
        FirebaseAuth firebaseAuth,
        ILogger<AuthService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _loginEvents = loginEvents;
        _failedLoginByIp = failedLoginByIp;
        _registrationStore = registrationStore;
        _tokenService = tokenService;
        _mfaService = mfaService;
        _walletProvisioning = walletProvisioning;
        _kafkaProducer = kafkaProducer;
        _firebaseAuth = firebaseAuth;
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
            AccountStatus = UserAccountStatus.Active,
            Role = UserRole.User,
            MfaEnabled = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var (rawRefreshToken, refreshTokenRecord) = CreateRefreshToken(user.Id, now);
        await _registrationStore.RegisterAsync(user, normalizedEmail, refreshTokenRecord);

        _logger.LogInformation("User registered: {UserId} ({Email}) with Role: {Role}, Status: {Status}",
            user.Id, user.Email, user.Role, user.AccountStatus);

        await _walletProvisioning.CreateWalletForUserAsync(user.Id);

        return (BuildAuthResponse(user, _tokenService.GenerateAccessToken(user)), rawRefreshToken);
    }

    public async Task<(AuthResponseDto response, string? rawRefreshToken)> LoginAsync(
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
            var failureReason = user is null ? "USER_NOT_FOUND" : "BAD_PASSWORD";

            if (loginEvent is not null)
            {
                loginEvent = loginEvent with
                {
                    Success = false,
                    FailureReason = user is null ? "UserNotFound" : "InvalidPassword"
                };
                await _failedLoginByIp.IncrementAsync(NormalizeIp(ipAddress), now);
            }

            var failedLoginMessage = BuildLoginEventMessage(
                eventId: loginEvent?.EventId ?? Guid.NewGuid(),
                userId: user?.Id,
                normalizedEmail: normalizedEmail,
                ipAddress: ipAddress,
                userAgent: userAgent,
                success: false,
                failureReason: failureReason,
                timestampUtc: now);

            _ = PublishLoginEventAsync(failedLoginMessage);

            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        if (user.AccountStatus != UserAccountStatus.Active)
            throw new UnauthorizedAccessException("Account is not active.");

        if (user.MfaEnabled)
        {
            var challenge = await _mfaService.CreateChallengeAsync(user.Id);
            return (BuildMfaChallengeResponse(user, challenge), null);
        }

        await _refreshTokens.RevokeAllActiveForUserAsync(user.Id);

        var (rawRefreshToken, refreshTokenRecord) = CreateRefreshToken(user.Id, now);
        await _refreshTokens.CreateAsync(refreshTokenRecord);

        if (loginEvent is not null)
        {
            loginEvent = loginEvent with { Success = true };
        }

        var successfulLoginMessage = BuildLoginEventMessage(
            eventId: loginEvent?.EventId ?? Guid.NewGuid(),
            userId: user.Id,
            normalizedEmail: normalizedEmail,
            ipAddress: ipAddress,
            userAgent: userAgent,
            success: true,
            failureReason: null,
            timestampUtc: now);

        _ = PublishLoginEventAsync(successfulLoginMessage);

        await _users.UpdateLastLoginAsync(user.Id, now);

        return (BuildAuthResponse(user, _tokenService.GenerateAccessToken(user)), rawRefreshToken);
    }

    public async Task<(AuthResponseDto response, string? rawRefreshToken)> GoogleLoginAsync(
        string idToken, string? ipAddress, string? userAgent)
    {
        try
        {
            var firebaseToken = await _firebaseAuth.VerifyIdTokenAsync(idToken);
            var firebaseUser = await _firebaseAuth.GetUserAsync(firebaseToken.Uid);

            var normalizedEmail = firebaseUser.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(normalizedEmail))
                throw new UnauthorizedAccessException("Google account email is missing.");

            var displayName = !string.IsNullOrWhiteSpace(firebaseUser.DisplayName)
                ? firebaseUser.DisplayName
                : normalizedEmail;

            // Extract profile picture URL from Firebase user (if available)
            var profilePictureUrl = !string.IsNullOrWhiteSpace(firebaseUser.PhotoUrl)
                ? firebaseUser.PhotoUrl
                : null;

            var user = await _users.GetByEmailAsync(normalizedEmail);
            var now = DateTime.UtcNow;

            var loginEvent = new LoginEventRecord(
                EventId: Guid.NewGuid(),
                UserId: user?.Id ?? Guid.Empty,
                UserEmail: normalizedEmail,
                UserName: displayName,
                IpAddress: ipAddress,
                Country: null,
                Success: false,
                FailureReason: null,
                IsFlagged: false,
                Timestamp: now,
                UserAgent: userAgent);

            if (user is null)
            {
                user = new User
                {
                    Email = normalizedEmail,
                    Name = displayName,
                    PasswordHash = string.Empty,
                    GoogleId = firebaseToken.Uid,
                    ProfilePictureUrl = profilePictureUrl,
                    CreatedAt = now,
                    UpdatedAt = now,
                    AccountStatus = UserAccountStatus.Active,
                    Role = UserRole.User
                };

                var (rawRefreshToken, refreshTokenRecord) = CreateRefreshToken(user.Id, now);
                await _registrationStore.RegisterAsync(user, normalizedEmail, refreshTokenRecord);

                _logger.LogInformation("User registered via Google: {UserId} ({Email})", user.Id, user.Email);

                await _walletProvisioning.CreateWalletForUserAsync(user.Id);

                loginEvent = loginEvent with { Success = true, UserId = user.Id };

                var googleLoginCreatedUserMessage = BuildLoginEventMessage(
                    eventId: loginEvent.EventId,
                    userId: user.Id,
                    normalizedEmail: normalizedEmail,
                    ipAddress: ipAddress,
                    userAgent: userAgent,
                    success: true,
                    failureReason: null,
                    timestampUtc: now);

                _ = PublishLoginEventAsync(googleLoginCreatedUserMessage);

                await _users.UpdateLastLoginAsync(user.Id, now);

                return (BuildAuthResponse(user, _tokenService.GenerateAccessToken(user)), rawRefreshToken);
            }

            if (user.AccountStatus != UserAccountStatus.Active)
                throw new UnauthorizedAccessException("Account is not active.");

            if (user.MfaEnabled)
            {
                var challenge = await _mfaService.CreateChallengeAsync(user.Id);
                return (BuildMfaChallengeResponse(user, challenge), null);
            }

            // Update profile picture if available (user may have changed it on Google)
            if (!string.IsNullOrWhiteSpace(profilePictureUrl))
            {
                user.ProfilePictureUrl = profilePictureUrl;
                user.UpdatedAt = now;
                await _users.UpdateAsync(user);
            }

            await _refreshTokens.RevokeAllActiveForUserAsync(user.Id);

            var (existingRawRefreshToken, existingRefreshTokenRecord) = CreateRefreshToken(user.Id, now);
            await _refreshTokens.CreateAsync(existingRefreshTokenRecord);

            loginEvent = loginEvent with { Success = true, UserId = user.Id };

            var googleLoginExistingUserMessage = BuildLoginEventMessage(
                eventId: loginEvent.EventId,
                userId: user.Id,
                normalizedEmail: normalizedEmail,
                ipAddress: ipAddress,
                userAgent: userAgent,
                success: true,
                failureReason: null,
                timestampUtc: now);

            _ = PublishLoginEventAsync(googleLoginExistingUserMessage);

            await _users.UpdateLastLoginAsync(user.Id, now);

            return (BuildAuthResponse(user, _tokenService.GenerateAccessToken(user)), existingRawRefreshToken);
        }
        catch (FirebaseAuthException ex)
        {
            _logger.LogWarning(ex, "Invalid Firebase token from Google login");
            throw new UnauthorizedAccessException("Invalid Google token.");
        }
        catch (UnauthorizedAccessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google login failed with exception type: {ExceptionType}", ex.GetType().Name);
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
            LastLoginAt: user.LastLoginAt,
            ProfilePictureUrl: user.ProfilePictureUrl,
            HasPassword: !string.IsNullOrWhiteSpace(user.PasswordHash),
            IsGoogleAccount: !string.IsNullOrWhiteSpace(user.GoogleId)
        );
    }

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

    public async Task UpdatePasswordAsync(Guid userId, UpdatePasswordRequestDto request)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new KeyNotFoundException($"User {userId} not found.");

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters long.");

        var hasPassword = !string.IsNullOrWhiteSpace(user.PasswordHash);
        if (hasPassword)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Current password is incorrect.");
            }
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.UpdatedAt = DateTime.UtcNow;

        await _users.UpdateAsync(user);

        _logger.LogInformation("User {UserId} password updated", userId);
    }

    public async Task DeleteUserAccountAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new KeyNotFoundException($"User {userId} not found.");

        user.AccountStatus = UserAccountStatus.Deleted;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.UpdateAsync(user);
        await _refreshTokens.RevokeAllActiveForUserAsync(userId);

        _logger.LogInformation("User {UserId} account deleted (soft)", userId);
    }

    public IReadOnlyList<SecurityQuestionCatalogDto> GetMfaQuestionCatalog() => _mfaService.GetQuestionCatalog();

    public async Task<MfaEnrollResponseDto> EnrollMfaAsync(Guid userId, MfaEnrollRequestDto request)
    {
        await _mfaService.EnableAsync(userId, request.Questions);
        return new MfaEnrollResponseDto(true, true, request.Questions.Count);
    }

    public async Task<MfaDisableResponseDto> DisableMfaAsync(Guid userId)
    {
        await _mfaService.DisableAsync(userId);
        return new MfaDisableResponseDto(true, false);
    }

    public async Task<(AuthResponseDto response, string rawRefreshToken)> VerifyMfaLoginAsync(
        MfaVerifyRequestDto request, string? ipAddress, string? userAgent)
    {
        var userId = await _mfaService.VerifyChallengeAsync(request.ChallengeToken, request.Answer);
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid MFA challenge.");

        if (user.AccountStatus != UserAccountStatus.Active)
            throw new UnauthorizedAccessException("Account is not active.");

        var now = DateTime.UtcNow;
        await _refreshTokens.RevokeAllActiveForUserAsync(user.Id);

        var (rawRefreshToken, refreshTokenRecord) = CreateRefreshToken(user.Id, now);
        await _refreshTokens.CreateAsync(refreshTokenRecord);

        var loginEvent = new LoginEventRecord(
            EventId: Guid.NewGuid(),
            UserId: user.Id,
            UserEmail: user.Email,
            UserName: user.Name,
            IpAddress: ipAddress,
            Country: null,
            Success: true,
            FailureReason: null,
            IsFlagged: false,
            Timestamp: now,
            UserAgent: userAgent);

        await _loginEvents.AddAsync(loginEvent);
        await _users.UpdateLastLoginAsync(user.Id, now);

        return (BuildAuthResponse(user, _tokenService.GenerateAccessToken(user)), rawRefreshToken);
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
            Role: user.Role.ToString(),
            ProfilePictureUrl: user.ProfilePictureUrl);

    private static AuthResponseDto BuildMfaChallengeResponse(User user, MfaChallengeResult challenge) =>
        new(
            AccessToken: string.Empty,
            TokenType: "Bearer",
            ExpiresIn: 0,
            UserId: user.Id,
            Email: user.Email,
            Name: user.Name,
            Role: user.Role.ToString(),
            ProfilePictureUrl: user.ProfilePictureUrl,
            MfaRequired: true,
            MfaChallengeToken: challenge.ChallengeToken,
            MfaQuestionId: challenge.QuestionId,
            MfaQuestionText: challenge.QuestionText,
            MfaExpiresAt: challenge.ExpiresAt);

    private LoginEventMessage BuildLoginEventMessage(
        Guid eventId,
        Guid? userId,
        string normalizedEmail,
        string? ipAddress,
        string? userAgent,
        bool success,
        string? failureReason,
        DateTime timestampUtc)
    {
        return new LoginEventMessage(
            EventId: eventId,
            UserId: userId?.ToString(),
            Email: normalizedEmail,
            IpAddress: string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress,
            CountryCode: null,
            Asn: null,
            UserAgent: string.IsNullOrWhiteSpace(userAgent) ? "unknown" : userAgent,
            DeviceFingerprint: null,
            Success: success,
            FailureReason: failureReason,
            TimestampUtc: timestampUtc);
    }

    private async Task PublishLoginEventAsync(LoginEventMessage loginEvent)
    {
        try
        {
            var key = !string.IsNullOrWhiteSpace(loginEvent.UserId)
                ? loginEvent.UserId
                : loginEvent.Email;

            await _kafkaProducer.ProduceAsync(
                KafkaTopics.LoginEvents,
                key,
                loginEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Kafka login-event publish failed. EventId={EventId} Email={Email}",
                loginEvent.EventId,
                loginEvent.Email);
        }
    }

    private static string NormalizeIp(string? ipAddress) =>
        string.IsNullOrWhiteSpace(ipAddress)
            ? "unknown"
            : ipAddress.Replace("/", "_");
}
