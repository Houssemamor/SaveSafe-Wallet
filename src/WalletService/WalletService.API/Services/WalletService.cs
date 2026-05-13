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
    Task<WalletTransferResponseDto> TransferFundsToWalletAsync(Guid senderUserId, string recipientWalletId, decimal amount, string? description);
    Task<string> ExportTransactionHistoryAsync(Guid userId, string? startDate, string? endDate);
    Task<InternalTopUpResponseDto> CreditTopUpAsync(InternalTopUpRequestDto request);
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

    /// <summary>Transfer funds directly to an existing recipient wallet.</summary>
    public async Task<WalletTransferResponseDto> TransferFundsToWalletAsync(
        Guid senderUserId, string recipientWalletId, decimal amount, string? description)
    {
        if (amount <= 0)
        {
            return new WalletTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Transfer amount must be greater than zero."
            };
        }

        if (!Guid.TryParse(recipientWalletId, out var recipientGuid))
        {
            return new WalletTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Invalid recipient wallet ID."
            };
        }

        var senderAccount = await _accounts.GetByUserIdAsync(senderUserId);
        if (senderAccount is null)
        {
            return new WalletTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Sender account not found."
            };
        }

        if (senderAccount.Balance < amount)
        {
            return new WalletTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Insufficient balance for transfer."
            };
        }

        var recipientAccount = await _accounts.GetByIdAsync(recipientGuid);
        if (recipientAccount is null)
        {
            return new WalletTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Recipient wallet not found."
            };
        }

        if (!recipientAccount.IsActive)
        {
            return new WalletTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Recipient wallet is inactive."
            };
        }

        if (recipientAccount.Id == senderAccount.Id)
        {
            return new WalletTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Cannot transfer to the same wallet."
            };
        }

        var transactionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        try
        {
            var senderLedgerEntry = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                AccountId = senderAccount.Id,
                Type = LedgerEntryType.Debit,
                Amount = -amount,
                BalanceAfter = senderAccount.Balance - amount,
                Description = description ?? $"Transfer to {recipientAccount.Name ?? recipientAccount.AccountNumber}",
                CreatedAt = now
            };

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

            senderAccount.Balance -= amount;
            senderAccount.LedgerCount++;
            senderAccount.UpdatedAt = now;

            recipientAccount.Balance += amount;
            recipientAccount.LedgerCount++;
            recipientAccount.UpdatedAt = now;

            await _accounts.UpdateAsync(senderAccount);
            await _accounts.UpdateAsync(recipientAccount);
            await _ledger.CreateAsync(senderLedgerEntry);
            await _ledger.CreateAsync(recipientLedgerEntry);

            return new WalletTransferResponseDto
            {
                Success = true,
                TransactionId = transactionId,
                NewBalance = senderAccount.Balance,
                RecipientName = recipientAccount.Name ?? recipientAccount.AccountNumber
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transfer to wallet failed for transaction {TransactionId}", transactionId);
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

    public async Task<InternalTopUpResponseDto> CreditTopUpAsync(InternalTopUpRequestDto request)
    {
        if (request.Amount <= 0)
        {
            return new InternalTopUpResponseDto(
                Success: false,
                ErrorMessage: "Top-up amount must be greater than zero.",
                WalletId: request.WalletId,
                NewBalance: 0,
                Currency: request.Currency,
                LedgerEntryId: null,
                Duplicate: false);
        }

        var account = await _accounts.GetByIdAsync(request.WalletId);
        if (account is null)
        {
            return new InternalTopUpResponseDto(
                Success: false,
                ErrorMessage: "Wallet not found.",
                WalletId: request.WalletId,
                NewBalance: 0,
                Currency: request.Currency,
                LedgerEntryId: null,
                Duplicate: false);
        }

        if (account.UserId != request.UserId.ToString())
        {
            return new InternalTopUpResponseDto(
                Success: false,
                ErrorMessage: "Wallet ownership mismatch.",
                WalletId: request.WalletId,
                NewBalance: 0,
                Currency: request.Currency,
                LedgerEntryId: null,
                Duplicate: false);
        }

        if (!account.IsActive)
        {
            return new InternalTopUpResponseDto(
                Success: false,
                ErrorMessage: "Wallet is inactive.",
                WalletId: request.WalletId,
                NewBalance: account.Balance,
                Currency: account.Currency ?? request.Currency,
                LedgerEntryId: null,
                Duplicate: false);
        }

        var referenceId = request.PaymentIntentId?.Trim();
        if (!string.IsNullOrWhiteSpace(referenceId))
        {
            var existing = await _ledger.GetByReferenceIdAsync(account.Id, referenceId);
            if (existing is not null)
            {
                return new InternalTopUpResponseDto(
                    Success: true,
                    ErrorMessage: null,
                    WalletId: account.Id,
                    NewBalance: existing.BalanceAfter,
                    Currency: account.Currency ?? request.Currency,
                    LedgerEntryId: existing.Id.ToString(),
                    Duplicate: true);
            }
        }

        var now = DateTime.UtcNow;
        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? account.Currency ?? "USD"
            : request.Currency.ToUpperInvariant();
        var newBalance = account.Balance + request.Amount;

        var ledgerEntry = new LedgerEntry
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            Type = LedgerEntryType.Credit,
            Amount = request.Amount,
            BalanceAfter = newBalance,
            Description = $"Stripe top-up ({request.StripeEventId})",
            ReferenceId = referenceId,
            CreatedAt = now
        };

        account.Balance = newBalance;
        account.LedgerCount++;
        account.UpdatedAt = now;

        await _ledger.CreateAsync(ledgerEntry);
        await _accounts.UpdateAsync(account);

        return new InternalTopUpResponseDto(
            Success: true,
            ErrorMessage: null,
            WalletId: account.Id,
            NewBalance: newBalance,
            Currency: currency,
            LedgerEntryId: ledgerEntry.Id.ToString(),
            Duplicate: false);
    }
}
