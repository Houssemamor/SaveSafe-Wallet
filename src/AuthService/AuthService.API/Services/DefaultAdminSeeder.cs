using AuthService.API.Data;
using AuthService.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.API.Services;

public interface IDefaultAdminSeeder
{
    Task SeedIfMissingAsync(CancellationToken cancellationToken = default);
}

public class DefaultAdminSeeder : IDefaultAdminSeeder
{
    private readonly AuthDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DefaultAdminSeeder> _logger;

    public DefaultAdminSeeder(
        AuthDbContext db,
        IConfiguration configuration,
        ILogger<DefaultAdminSeeder> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedIfMissingAsync(CancellationToken cancellationToken = default)
    {
        var hasAdmin = await _db.Users.AnyAsync(u => u.Role == UserRole.Admin, cancellationToken);
        if (hasAdmin)
        {
            return;
        }

        var configuredEmail = (_configuration["DefaultAdmin:Email"] ?? "admin@savesafe.local").Trim().ToLowerInvariant();
        var configuredName = (_configuration["DefaultAdmin:Name"] ?? "Default Administrator").Trim();
        var configuredPassword = _configuration["DefaultAdmin:Password"] ?? "Admin@12345!";

        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == configuredEmail, cancellationToken);
        if (existingUser is not null)
        {
            existingUser.Role = UserRole.Admin;
            existingUser.AccountStatus = UserAccountStatus.Active;
            existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(configuredPassword, workFactor: 12);
            existingUser.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "No admin account existed. Existing user {Email} has been promoted to Admin using configured default credentials.",
                configuredEmail);

            return;
        }

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

        _db.Users.Add(adminUser);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "No admin account existed. A default admin account has been created: {Email}. Change this password immediately.",
            configuredEmail);
    }
}
