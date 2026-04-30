using WalletService.API.DTOs;
using WalletService.API.Entities;
using WalletService.API.Persistence;

namespace WalletService.API.Services;

public interface IWalletService
{
    Task<Guid> CreateAccountAsync(Guid userId, string currency = "USD");
    Task<WalletBalanceResponseDto?> GetBalanceAsync(Guid userId);
    Task<WalletHistoryResponseDto?> GetWalletHistoryAsync(Guid userId, string? pageToken, int pageSize = 10);
}

public class WalletService : IWalletService
{
    private readonly IAccountRepository _accounts;
    private readonly ILedgerRepository _ledger;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        IAccountRepository accounts,
        ILedgerRepository ledger,
        ILogger<WalletService> logger)
    {
        _accounts = accounts;
        _ledger = ledger;
        _logger = logger;
    }

    public async Task<Guid> CreateAccountAsync(Guid userId, string currency = "USD")
    {
        // Idempotent: ensure a single account per user via the user->account index.
        var result = await _accounts.GetOrCreateAsync(userId, currency);

        if (result.Created)
        {
            _logger.LogInformation(
                "Account {AccountNumber} created for user {UserId}",
                result.AccountNumber, userId);
        }
        else
        {
            _logger.LogInformation(
                "Account already exists for user {UserId}, returning existing {AccountId}",
                userId, result.AccountId);
        }

        return result.AccountId;
    }

    public async Task<WalletBalanceResponseDto?> GetBalanceAsync(Guid userId)
    {
        var account = await _accounts.GetByUserIdAsync(userId);

        if (account is null) return null;

        return new WalletBalanceResponseDto(
            AccountId: account.Id,
            AccountNumber: account.AccountNumber,
            Currency: account.Currency,
            Balance: account.Balance,
            UpdatedAt: account.UpdatedAt
        );
    }

    /// <summary>Retrieve paginated wallet transaction history (ledger entries). Used by GET /api/wallet/history.</summary>
    public async Task<WalletHistoryResponseDto?> GetWalletHistoryAsync(
        Guid userId, string? pageToken, int pageSize = 10)
    {
        var account = await _accounts.GetByUserIdAsync(userId);

        if (account is null) return null;

        var page = await _ledger.GetPageAsync(account.Id, pageSize, pageToken);

        var entries = page.Entries
            .Select(le => new LedgerHistoryItemDto(
                le.Id,
                le.Type.ToString(),
                le.Amount,
                le.BalanceAfter,
                le.Description,
                le.CreatedAt
            ))
            .ToList();

        return new WalletHistoryResponseDto(
            Entries: entries,
            PageSize: pageSize,
            TotalCount: (int)account.LedgerCount,
            NextPageToken: page.NextPageToken
        );
    }
}
