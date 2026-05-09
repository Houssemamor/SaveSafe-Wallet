namespace AuthService.API.Services;

public interface IUserService
{
    Task<Guid?> GetUserIdByEmailAsync(string email);
    Task<string?> GetUserNameAsync(Guid userId);
}