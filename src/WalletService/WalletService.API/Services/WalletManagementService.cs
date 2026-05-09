using WalletService.API.DTOs;
using WalletService.API.Persistence;

namespace WalletService.API.Services;

/// <summary>
/// Implementation of wallet management service
/// Handles business logic for wallet CRUD operations
/// </summary>
public class WalletManagementService : IWalletManagementService
{
    private readonly IAccountRepository _accountRepository;
    private readonly ILedgerRepository _ledgerRepository;

    public WalletManagementService(
        IAccountRepository accountRepository,
        ILedgerRepository ledgerRepository)
    {
        _accountRepository = accountRepository;
        _ledgerRepository = ledgerRepository;
    }

    /// <summary>
    /// Get all wallets for the authenticated user
    /// </summary>
    public async Task<IEnumerable<WalletResponseDto>> GetUserWalletsAsync(string userId)
    {
        var accounts = await _accountRepository.GetAccountsByUserIdAsync(userId);

        return accounts.Select(account => new WalletResponseDto
        {
            Id = account.Id.ToString(),
            Name = account.Name ?? "Default Wallet",
            Type = account.Type ?? "checking",
            Balance = account.Balance,
            Currency = account.Currency ?? "USD",
            IsActive = account.IsActive,
            CreatedAt = account.CreatedAt,
            IsDefault = account.IsDefault
        });
    }

    /// <summary>
    /// Create a new wallet for the user
    /// </summary>
    public async Task<CreateWalletResponseDto> CreateWalletAsync(string userId, CreateManagedWalletRequestDto request)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new CreateWalletResponseDto
            {
                Success = false,
                ErrorMessage = "Wallet name is required."
            };
        }

        if (request.Name.Length < 2 || request.Name.Length > 50)
        {
            return new CreateWalletResponseDto
            {
                Success = false,
                ErrorMessage = "Wallet name must be between 2 and 50 characters."
            };
        }

        // Validate wallet type
        var validTypes = new[] { "checking", "savings", "investment", "reserve" };
        var requestTypeLower = request.Type?.ToLower() ?? "checking";
        if (!validTypes.Contains(requestTypeLower))
        {
            return new CreateWalletResponseDto
            {
                Success = false,
                ErrorMessage = "Invalid wallet type. Must be checking, savings, investment, or reserve."
            };
        }

        // Check if user already has a wallet with the same name
        var existingAccounts = await _accountRepository.GetAccountsByUserIdAsync(userId);
        if (existingAccounts.Any(a => a.Name?.Equals(request.Name, StringComparison.OrdinalIgnoreCase) == true))
        {
            return new CreateWalletResponseDto
            {
                Success = false,
                ErrorMessage = "A wallet with this name already exists."
            };
        }

        // Determine if this should be the default wallet
        var isFirstWallet = !existingAccounts.Any();
        var isDefault = isFirstWallet;

        // Create the wallet account
        var account = new Entities.Account
        {
            UserId = userId,
            Name = request.Name,
            Type = requestTypeLower,
            Currency = request.Currency?.ToUpper() ?? "USD",
            Balance = request.InitialBalance ?? 0,
            IsActive = true,
            IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow
        };

        var createdAccount = await _accountRepository.CreateAccountAsync(account);

        // If initial balance was provided, create a ledger entry
        if (request.InitialBalance.HasValue && request.InitialBalance.Value > 0)
        {
            var ledgerEntry = new Entities.LedgerEntry
            {
                AccountId = createdAccount.Id,
                Amount = request.InitialBalance.Value,
                Type = Entities.LedgerEntryType.Credit,
                Description = "Initial deposit",
                CreatedAt = DateTime.UtcNow
            };

            await _ledgerRepository.CreateAsync(ledgerEntry);
        }

        return new CreateWalletResponseDto
        {
            Success = true,
            Wallet = new WalletResponseDto
            {
                Id = createdAccount.Id.ToString(),
                Name = createdAccount.Name ?? "Default Wallet",
                Type = createdAccount.Type ?? "checking",
                Balance = createdAccount.Balance,
                Currency = createdAccount.Currency ?? "USD",
                IsActive = createdAccount.IsActive,
                CreatedAt = createdAccount.CreatedAt,
                IsDefault = createdAccount.IsDefault
            }
        };
    }

    /// <summary>
    /// Delete a wallet by ID
    /// </summary>
    public async Task DeleteWalletAsync(string userId, string walletId)
    {
        // Get the wallet to validate ownership
        var account = await _accountRepository.GetAccountByIdAsync(walletId);

        if (account == null)
        {
            throw new KeyNotFoundException("Wallet not found.");
        }

        if (account.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not have permission to delete this wallet.");
        }

        // Cannot delete default wallet
        if (account.IsDefault)
        {
            throw new InvalidOperationException("Cannot delete default wallet. Set another wallet as default first.");
        }

        // Cannot delete wallet with non-zero balance
        if (account.Balance != 0)
        {
            throw new InvalidOperationException("Cannot delete wallet with non-zero balance.");
        }

        // Cannot delete if it's the only wallet
        var allAccounts = await _accountRepository.GetAccountsByUserIdAsync(userId);
        if (allAccounts.Count() <= 1)
        {
            throw new InvalidOperationException("Cannot delete the only wallet. Create another wallet first.");
        }

        // Soft delete by setting IsActive to false
        await _accountRepository.DeactivateAccountAsync(walletId);
    }

    /// <summary>
    /// Set a wallet as the default wallet for the user
    /// </summary>
    public async Task SetDefaultWalletAsync(string userId, string walletId)
    {
        // Get the wallet to validate ownership
        var account = await _accountRepository.GetAccountByIdAsync(walletId);

        if (account == null)
        {
            throw new KeyNotFoundException("Wallet not found.");
        }

        if (account.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not have permission to modify this wallet.");
        }

        if (!account.IsActive)
        {
            throw new InvalidOperationException("Cannot set inactive wallet as default.");
        }

        if (account.IsDefault)
        {
            return; // Already default
        }

        // Unset current default wallet
        var currentDefault = await _accountRepository.GetDefaultAccountAsync(userId);
        if (currentDefault != null && currentDefault.Id.ToString() != walletId)
        {
            await _accountRepository.UnsetDefaultWalletAsync(currentDefault.Id.ToString());
        }

        // Set new default wallet
        await _accountRepository.SetDefaultWalletAsync(walletId);
    }

    /// <summary>
    /// Get balance for a specific wallet
    /// </summary>
    public async Task<WalletBalanceResponseDto> GetWalletBalanceAsync(string userId, string walletId)
    {
        var account = await _accountRepository.GetAccountByIdAsync(walletId);

        if (account == null)
        {
            throw new KeyNotFoundException("Wallet not found.");
        }

        if (account.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not have permission to access this wallet.");
        }

        return new WalletBalanceResponseDto(
            account.Id,
            account.AccountNumber,
            account.Currency ?? "USD",
            account.Balance,
            account.UpdatedAt
        );
    }

    /// <summary>
    /// Transfer funds between user's own wallets
    /// </summary>
    public async Task<InternalTransferResponseDto> TransferBetweenWalletsAsync(
        string userId,
        string sourceWalletId,
        string targetWalletId,
        decimal amount)
    {
        // Validate amount
        if (amount <= 0)
        {
            return new InternalTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Transfer amount must be greater than zero."
            };
        }

        // Cannot transfer to same wallet
        if (sourceWalletId == targetWalletId)
        {
            return new InternalTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Source and target wallets cannot be the same."
            };
        }

        // Parse wallet IDs as Guids
        if (!Guid.TryParse(sourceWalletId, out var sourceGuid))
        {
            return new InternalTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Invalid source wallet ID."
            };
        }

        if (!Guid.TryParse(targetWalletId, out var targetGuid))
        {
            return new InternalTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Invalid target wallet ID."
            };
        }

        // Get source wallet
        var sourceAccount = await _accountRepository.GetAccountByIdAsync(sourceWalletId);
        if (sourceAccount == null)
        {
            return new InternalTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Source wallet not found."
            };
        }

        if (sourceAccount.UserId != userId)
        {
            return new InternalTransferResponseDto
            {
                Success = false,
                ErrorMessage = "You do not have permission to access the source wallet."
            };
        }

        // Get target wallet
        var targetAccount = await _accountRepository.GetAccountByIdAsync(targetWalletId);
        if (targetAccount == null)
        {
            return new InternalTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Target wallet not found."
            };
        }

        if (targetAccount.UserId != userId)
        {
            return new InternalTransferResponseDto
            {
                Success = false,
                ErrorMessage = "You do not have permission to access the target wallet."
            };
        }

        // Check sufficient balance
        if (sourceAccount.Balance < amount)
        {
            return new InternalTransferResponseDto
            {
                Success = false,
                ErrorMessage = "Insufficient balance in source wallet."
            };
        }

        // Perform the transfer
        try
        {
            // Create debit entry for source wallet
            var debitEntry = new Entities.LedgerEntry
            {
                AccountId = sourceGuid,
                Amount = -amount,
                Type = Entities.LedgerEntryType.Debit,
                Description = $"Transfer to wallet: {targetAccount.Name}",
                CreatedAt = DateTime.UtcNow
            };

            await _ledgerRepository.CreateAsync(debitEntry);

            // Create credit entry for target wallet
            var creditEntry = new Entities.LedgerEntry
            {
                AccountId = targetGuid,
                Amount = amount,
                Type = Entities.LedgerEntryType.Credit,
                Description = $"Transfer from wallet: {sourceAccount.Name}",
                CreatedAt = DateTime.UtcNow
            };

            await _ledgerRepository.CreateAsync(creditEntry);

            // Update account balances
            sourceAccount.Balance -= amount;
            targetAccount.Balance += amount;

            await _accountRepository.UpdateAsync(sourceAccount);
            await _accountRepository.UpdateAsync(targetAccount);

            return new InternalTransferResponseDto
            {
                Success = true,
                TransactionId = Guid.NewGuid().ToString()
            };
        }
        catch (Exception ex)
        {
            return new InternalTransferResponseDto
            {
                Success = false,
                ErrorMessage = $"Transfer failed: {ex.Message}"
            };
        }
    }
}