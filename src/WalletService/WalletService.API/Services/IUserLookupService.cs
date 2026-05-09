namespace WalletService.API.Services;

public interface IUserLookupService
{
    Task<Guid?> GetUserIdByEmailAsync(string email);
    Task<string?> GetUserNameAsync(Guid userId);
}