using WalletService.API.Entities;

namespace WalletService.API.Persistence;

public sealed record AccountCreateResult(
    Guid AccountId,
    string AccountNumber,
    bool Created);

public interface IAccountRepository
{
    /// <summary>
    /// Get account by user ID (legacy single account method)
    /// </summary>
    Task<Account?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get account by account ID
    /// </summary>
    Task<Account?> GetByIdAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Get or create account for user (legacy single account method)
    /// </summary>
    Task<AccountCreateResult> GetOrCreateAsync(Guid userId, string currency, CancellationToken ct = default);

    /// <summary>
    /// Update account
    /// </summary>
    Task UpdateAsync(Account account, CancellationToken ct = default);

    /// <summary>
    /// Get all accounts for a user (multi-wallet support)
    /// </summary>
    Task<IEnumerable<Account>> GetAccountsByUserIdAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Get account by ID (string version for wallet management)
    /// </summary>
    Task<Account?> GetAccountByIdAsync(string accountId, CancellationToken ct = default);

    /// <summary>
    /// Create a new account
    /// </summary>
    Task<Account> CreateAccountAsync(Account account, CancellationToken ct = default);

    /// <summary>
    /// Deactivate an account (soft delete)
    /// </summary>
    Task DeactivateAccountAsync(string accountId, CancellationToken ct = default);

    /// <summary>
    /// Get default account for user
    /// </summary>
    Task<Account?> GetDefaultAccountAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Set account as default wallet
    /// </summary>
    Task SetDefaultWalletAsync(string accountId, CancellationToken ct = default);

    /// <summary>
    /// Unset default wallet status
    /// </summary>
    Task UnsetDefaultWalletAsync(string accountId, CancellationToken ct = default);
}
