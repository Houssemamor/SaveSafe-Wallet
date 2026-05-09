using WalletService.API.DTOs;
using WalletService.API.Entities;
using WalletService.API.Persistence;

namespace WalletService.API.Services;

public interface IWalletService
{
    Task<Guid> CreateAccountAsync(Guid userId, string currency = "USD");
    Task<WalletBalanceResponseDto?> GetBalanceAsync(Guid userId);
    Task<WalletHistoryResponseDto?> GetWalletHistoryAsync(Guid userId, string? pageToken, int pageSize = 10);
    Task<WalletTransferResponseDto> TransferFundsAsync(Guid senderUserId, string recipientEmail, decimal amount, string? description);
    Task<string> ExportTransactionHistoryAsync(Guid userId, string? startDate, string? endDate);
}

public class WalletService : IWalletService
{
    private readonly IAccountRepository _accounts;
    private readonly ILedgerRepository _ledger;
    private readonly IUserLookupService _userLookupService;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        IAccountRepository accounts,
        ILedgerRepository ledger,
        IUserLookupService userLookupService,
        ILogger<WalletService> logger)
    {
        _accounts = accounts;
        _ledger = ledger;
        _userLookupService = userLookupService;
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

    /// <summary>Transfer funds to another user's wallet.</summary>
    public async Task<WalletTransferResponseDto> TransferFundsAsync(
        Guid senderUserId, string recipientEmail, decimal amount, string? description)
    {
        // Validate amount
        if (amount <= 0)
        {
            return new WalletTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Transfer amount must be greater than zero."
            };
        }

        // Get sender account
        var senderAccount = await _accounts.GetByUserIdAsync(senderUserId);
        if (senderAccount is null)
        {
            return new WalletTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Sender account not found."
            };
        }

        // Check sufficient balance
        if (senderAccount.Balance < amount)
        {
            return new WalletTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Insufficient balance for transfer."
            };
        }

        // Look up recipient user by email
        _logger.LogInformation("Looking up recipient user by email: {Email}", recipientEmail);
        var recipientUserId = await _userLookupService.GetUserIdByEmailAsync(recipientEmail);
        if (recipientUserId is null)
        {
            return new WalletTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Recipient user not found."
            };
        }

        // Prevent self-transfer
        if (recipientUserId == senderUserId)
        {
            return new WalletTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Cannot transfer to yourself."
            };
        }

        // Get or create recipient account
        var recipientAccountId = await _accounts.GetOrCreateAsync(recipientUserId.Value, senderAccount.Currency);
        var recipientAccount = await _accounts.GetByIdAsync(recipientAccountId.AccountId);
        if (recipientAccount is null)
        {
            return new WalletTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Recipient account not found."
            };
        }

        // Perform transfer
        var transactionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        try
        {
            // Debit sender
            var senderLedgerEntry = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                AccountId = senderAccount.Id,
                Type = LedgerEntryType.Debit,
                Amount = -amount,
                BalanceAfter = senderAccount.Balance - amount,
                Description = description ?? $"Transfer to {recipientEmail}",
                CreatedAt = now
            };

            // Credit recipient
            var recipientLedgerEntry = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                AccountId = recipientAccount.Id,
                Type = LedgerEntryType.Credit,
                Amount = amount,
                BalanceAfter = recipientAccount.Balance + amount,
                Description = description ?? $"Transfer from {senderAccount.AccountNumber}",
                CreatedAt = now
            };

            // Update balances
            senderAccount.Balance -= amount;
            senderAccount.LedgerCount++;
            senderAccount.UpdatedAt = now;

            recipientAccount.Balance += amount;
            recipientAccount.LedgerCount++;
            recipientAccount.UpdatedAt = now;

            // Save all changes
            await _accounts.UpdateAsync(senderAccount);
            await _accounts.UpdateAsync(recipientAccount);
            await _ledger.CreateAsync(senderLedgerEntry);
            await _ledger.CreateAsync(recipientLedgerEntry);

            _logger.LogInformation(
                "Transfer of {Amount} from {SenderAccountId} to {RecipientAccountId} completed successfully. Transaction ID: {TransactionId}",
                amount, senderAccount.Id, recipientAccount.Id, transactionId);

            return new WalletTransferResponseDto
            {
                Success = true,
                TransactionId = transactionId,
                NewBalance = senderAccount.Balance,
                RecipientName = recipientEmail // In production, get actual recipient name
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transfer failed for transaction {TransactionId}", transactionId);
            return new WalletTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Transfer failed due to a system error. Please try again."
            };
        }
    }

    /// <summary>Export wallet transaction history as CSV.</summary>
    public async Task<string> ExportTransactionHistoryAsync(Guid userId, string? startDate, string? endDate)
    {
        var account = await _accounts.GetByUserIdAsync(userId);
        if (account is null) return string.Empty;

        // Get all entries (no pagination for export)
        var allEntries = await _ledger.GetAllByAccountIdAsync(account.Id);

        // Filter by date range if provided
        var filteredEntries = allEntries.AsEnumerable();

        if (!string.IsNullOrEmpty(startDate) && DateTime.TryParse(startDate, out var start))
        {
            filteredEntries = filteredEntries.Where(e => e.CreatedAt >= start);
        }

        if (!string.IsNullOrEmpty(endDate) && DateTime.TryParse(endDate, out var end))
        {
            filteredEntries = filteredEntries.Where(e => e.CreatedAt <= end.AddDays(1).AddTicks(-1));
        }

        // Generate CSV
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Date,Type,Amount,Balance After,Description");

        foreach (var entry in filteredEntries.OrderByDescending(e => e.CreatedAt))
        {
            csv.AppendLine($"{entry.CreatedAt:yyyy-MM-dd HH:mm:ss},{entry.Type},{entry.Amount:F2},{entry.BalanceAfter:F2},\"{entry.Description?.Replace("\"", "\"\"")}\"");
        }

        return csv.ToString();
    }
}
