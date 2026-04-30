using WalletService.API.Entities;

namespace WalletService.API.Persistence;

public sealed record AccountCreateResult(
    Guid AccountId,
    string AccountNumber,
    bool Created);

public interface IAccountRepository
{
    Task<Account?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Account?> GetByIdAsync(Guid accountId, CancellationToken ct = default);
    Task<AccountCreateResult> GetOrCreateAsync(Guid userId, string currency, CancellationToken ct = default);
}
