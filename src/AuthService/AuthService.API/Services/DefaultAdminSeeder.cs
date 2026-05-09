using AuthService.API.Entities;
using AuthService.API.Persistence;

namespace AuthService.API.Services;

public interface IDefaultAdminSeeder
{
    Task SeedIfMissingAsync(CancellationToken cancellationToken = default);
}

public class DefaultAdminSeeder : IDefaultAdminSeeder
{
    private readonly IUserRepository _users;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DefaultAdminSeeder> _logger;

    public DefaultAdminSeeder(
        IUserRepository users,
        IConfiguration configuration,
        ILogger<DefaultAdminSeeder> logger)
    {
        _users = users;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedIfMissingAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking if admin user exists...");

        var hasAdmin = await _users.AnyWithRoleAsync(UserRole.Admin, cancellationToken);
        if (hasAdmin)
        {
            _logger.LogInformation("Admin user already exists, skipping seeding");
            return;
        }

        _logger.LogWarning("No admin user found, proceeding with admin seeding");

        var configuredEmail = (_configuration["DefaultAdmin:Email"] ?? "admin@savesafe.local").Trim().ToLowerInvariant();
        var configuredName = (_configuration["DefaultAdmin:Name"] ?? "Default Administrator").Trim();
        var configuredPassword = _configuration["DefaultAdmin:Password"] ?? "Admin@12345!";

        _logger.LogInformation("Configured admin email: {Email}", configuredEmail);

        var existingUser = await _users.GetByEmailAsync(configuredEmail, cancellationToken);
        if (existingUser is not null)
        {
            _logger.LogInformation("Found existing user with email {Email}, promoting to admin", configuredEmail);

            existingUser.Role = UserRole.Admin;
            existingUser.AccountStatus = UserAccountStatus.Active;
            existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(configuredPassword, workFactor: 12);
            existingUser.UpdatedAt = DateTime.UtcNow;

            await _users.UpdateAsync(existingUser, cancellationToken);

            _logger.LogWarning(
                "No admin account existed. Existing user {Email} has been promoted to Admin using configured default credentials.",
                configuredEmail);

            return;
        }

        _logger.LogInformation("Creating new admin user with email {Email}", configuredEmail);

        var adminUser = new User
        {
            Email = configuredEmail,
            Name = configuredName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(configuredPassword, workFactor: 12),
            Role = UserRole.Admin,
            AccountStatus = UserAccountStatus.Active,
            MfaEnabled = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _users.CreateAsync(adminUser, configuredEmail, cancellationToken);

        _logger.LogWarning(
            "No admin account existed. A default admin account has been created: {Email}. Change this password immediately.",
            configuredEmail);
    }
}
