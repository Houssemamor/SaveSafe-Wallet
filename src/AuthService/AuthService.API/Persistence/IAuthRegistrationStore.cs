using AuthService.API.Entities;

namespace AuthService.API.Persistence;

public interface IAuthRegistrationStore
{
    Task RegisterAsync(
        User user,
        string normalizedEmail,
        RefreshTokenRecord refreshToken,
        CancellationToken ct = default);
}
