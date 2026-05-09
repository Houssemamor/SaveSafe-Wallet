using AuthService.API.Persistence;

namespace AuthService.API.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository userRepository, ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Guid?> GetUserIdByEmailAsync(string email)
    {
        try
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();
            _logger.LogDebug("Looking up user ID for email: {Email}", normalizedEmail);

            var user = await _userRepository.GetByEmailAsync(normalizedEmail);

            if (user is null)
            {
                _logger.LogWarning("User not found for email: {Email}", normalizedEmail);
                return null;
            }

            _logger.LogDebug("Found user ID {UserId} for email: {Email}", user.Id, normalizedEmail);
            return user.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up user ID for email: {Email}", email);
            return null;
        }
    }

    public async Task<string?> GetUserNameAsync(Guid userId)
    {
        try
        {
            _logger.LogDebug("Looking up user name for user ID: {UserId}", userId);

            var user = await _userRepository.GetByIdAsync(userId);

            if (user is null)
            {
                _logger.LogWarning("User not found for ID: {UserId}", userId);
                return null;
            }

            _logger.LogDebug("Found user name {UserName} for user ID: {UserId}", user.Name, userId);
            return user.Name;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up user name for user ID: {UserId}", userId);
            return null;
        }
    }
}