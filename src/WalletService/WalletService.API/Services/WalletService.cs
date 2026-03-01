using Microsoft.EntityFrameworkCore;
using WalletService.API.Data;
using WalletService.API.DTOs;
using WalletService.API.Entities;

namespace WalletService.API.Services;

public interface IWalletService
{
    Task<Guid> CreateAccountAsync(Guid userId, string currency = "USD");
    Task<WalletBalanceResponseDto?> GetBalanceAsync(Guid userId);
}

public class WalletService : IWalletService
{
    private readonly WalletDbContext _db;
    private readonly ILogger<WalletService> _logger;

    public WalletService(WalletDbContext db, ILogger<WalletService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Guid> CreateAccountAsync(Guid userId, string currency = "USD")
    {
        // Idempotent: if account already exists, return its id
        var existing = await _db.Accounts
            .FirstOrDefaultAsync(a => a.UserId == userId);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Account already exists for user {UserId}, returning existing {AccountId}",
                userId, existing.Id);
            return existing.Id;
        }

        // Generate sequential account number
        var count = await _db.Accounts.CountAsync();
        var accountNumber = $"SSW-{(count + 1):D10}";

        var account = new Account
        {
            UserId = userId,
            AccountNumber = accountNumber,
            Type = AccountType.Savings,
            Currency = currency.ToUpperInvariant(),
            Balance = 0.00m
        };
        _db.Accounts.Add(account);

        // Opening ledger entry - records that the account was created at zero balance
        var openingEntry = new LedgerEntry
        {
            AccountId = account.Id,
            Type = LedgerEntryType.Credit,
            Amount = 0.00m,
            BalanceAfter = 0.00m,
            Description = "Account opened"
        };
        _db.LedgerEntries.Add(openingEntry);

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Account {AccountNumber} created for user {UserId}", accountNumber, userId);

        return account.Id;
    }

    public async Task<WalletBalanceResponseDto?> GetBalanceAsync(Guid userId)
    {
        var account = await _db.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (account is null) return null;

        return new WalletBalanceResponseDto(
            AccountId: account.Id,
            AccountNumber: account.AccountNumber,
            Currency: account.Currency,
            Balance: account.Balance,
            UpdatedAt: account.UpdatedAt
        );
    }
}
