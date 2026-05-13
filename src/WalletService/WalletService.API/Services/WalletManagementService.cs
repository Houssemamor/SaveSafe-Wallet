using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration? _configuration;

    public WalletManagementService(
        IAccountRepository accountRepository,
        ILedgerRepository ledgerRepository,
        IConfiguration? configuration = null)
    {
        _accountRepository = accountRepository;
        _ledgerRepository = ledgerRepository;
        _configuration = configuration;
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

    /// <summary>
    /// Create a signed QR token for receiving funds at a wallet.
    /// The token only identifies the wallet; it does not expose secrets.
    /// </summary>
    public async Task<ReceiveWalletQrResponseDto> CreateReceiveQrAsync(string userId, string? walletId = null)
    {
        var account = await ResolveWalletForReceiveQrAsync(userId, walletId);
        if (account is null)
        {
            return new ReceiveWalletQrResponseDto
            {
                Success = false,
                ErrorMessage = "No active wallet was found for QR generation."
            };
        }

        if (!TryGetQrSigningKey(out var signingKey))
        {
            return new ReceiveWalletQrResponseDto
            {
                Success = false,
                ErrorMessage = "QR signing key is not configured."
            };
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        var payload = new ReceiveQrTokenPayload(
            WalletId: account.Id.ToString(),
            UserId: userId,
            Purpose: ReceiveQrPurpose,
            ExpiresAtUnixSeconds: expiresAt.ToUnixTimeSeconds(),
            Nonce: Guid.NewGuid().ToString("N"));

        return new ReceiveWalletQrResponseDto
        {
            Success = true,
            Token = ReceiveQrTokenCodec.Create(payload, signingKey),
            WalletId = account.Id.ToString(),
            WalletName = account.Name ?? "Default Wallet",
            Currency = account.Currency ?? "USD",
            ExpiresAt = expiresAt.UtcDateTime
        };
    }

    /// <summary>
    /// Resolve a signed receive QR token into wallet details.
    /// The caller can use the returned wallet ID to submit a transfer.
    /// </summary>
    public async Task<ResolveWalletQrResponseDto> ResolveReceiveQrAsync(string token)
    {
        if (!TryGetQrSigningKey(out var signingKey))
        {
            return new ResolveWalletQrResponseDto
            {
                Success = false,
                ErrorMessage = "QR signing key is not configured."
            };
        }

        if (!ReceiveQrTokenCodec.TryValidate(token, signingKey, out var payload, out var validationError))
        {
            return new ResolveWalletQrResponseDto
            {
                Success = false,
                ErrorMessage = validationError
            };
        }

        if (!string.Equals(payload.Purpose, ReceiveQrPurpose, StringComparison.Ordinal))
        {
            return new ResolveWalletQrResponseDto
            {
                Success = false,
                ErrorMessage = "The scanned QR code is not a valid receive token."
            };
        }

        var account = await _accountRepository.GetAccountByIdAsync(payload.WalletId);
        if (account is null || account.UserId != payload.UserId)
        {
            return new ResolveWalletQrResponseDto
            {
                Success = false,
                ErrorMessage = "The scanned QR code no longer matches an active wallet."
            };
        }

        if (!account.IsActive)
        {
            return new ResolveWalletQrResponseDto
            {
                Success = false,
                ErrorMessage = "The scanned wallet is inactive."
            };
        }

        return new ResolveWalletQrResponseDto
        {
            Success = true,
            WalletId = account.Id.ToString(),
            WalletName = account.Name ?? "Default Wallet",
            Currency = account.Currency ?? "USD"
        };
    }

    private async Task<Entities.Account?> ResolveWalletForReceiveQrAsync(string userId, string? walletId)
    {
        if (!string.IsNullOrWhiteSpace(walletId))
        {
            var selectedWallet = await _accountRepository.GetAccountByIdAsync(walletId);
            if (selectedWallet is not null && selectedWallet.UserId == userId && selectedWallet.IsActive)
            {
                return selectedWallet;
            }

            return null;
        }

        var defaultWallet = await _accountRepository.GetDefaultAccountAsync(userId);
        if (defaultWallet is not null && defaultWallet.IsActive)
        {
            return defaultWallet;
        }

        var accounts = await _accountRepository.GetAccountsByUserIdAsync(userId);
        return accounts.FirstOrDefault(account => account.IsActive);
    }

    private bool TryGetQrSigningKey(out string signingKey)
    {
        signingKey = _configuration?["Jwt:Key"] ?? string.Empty;
        return !string.IsNullOrWhiteSpace(signingKey);
    }

    private const string ReceiveQrPurpose = "wallet-receive";

    private sealed record ReceiveQrTokenPayload(
        string WalletId,
        string UserId,
        string Purpose,
        long ExpiresAtUnixSeconds,
        string Nonce);

    private static class ReceiveQrTokenCodec
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public static string Create(ReceiveQrTokenPayload payload, string signingKey)
        {
            var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
            var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
            var signature = CreateSignature(encodedPayload, signingKey);
            return string.Join('.', "sswqr", encodedPayload, signature);
        }

        public static bool TryValidate(
            string token,
            string signingKey,
            out ReceiveQrTokenPayload payload,
            out string errorMessage)
        {
            payload = default!;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(token))
            {
                errorMessage = "QR token is required.";
                return false;
            }

            var segments = token.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length != 3 || !string.Equals(segments[0], "sswqr", StringComparison.Ordinal))
            {
                errorMessage = "Invalid QR token format.";
                return false;
            }

            var expectedSignature = CreateSignature(segments[1], signingKey);
            if (!FixedTimeEquals(expectedSignature, segments[2]))
            {
                errorMessage = "QR token signature is invalid.";
                return false;
            }

            if (!TryBase64UrlDecode(segments[1], out var payloadBytes))
            {
                errorMessage = "QR token payload is invalid.";
                return false;
            }

            try
            {
                payload = JsonSerializer.Deserialize<ReceiveQrTokenPayload>(payloadBytes, JsonOptions)
                    ?? throw new JsonException("Missing payload.");
            }
            catch
            {
                errorMessage = "QR token payload could not be read.";
                return false;
            }

            if (payload.ExpiresAtUnixSeconds < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                errorMessage = "QR token has expired.";
                return false;
            }

            return true;
        }

        private static string CreateSignature(string payload, string signingKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Base64UrlEncode(hash);
        }

        private static bool TryBase64UrlDecode(string value, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();

            var normalized = value.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');

            try
            {
                bytes = Convert.FromBase64String(normalized);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            var leftBytes = Encoding.UTF8.GetBytes(left);
            var rightBytes = Encoding.UTF8.GetBytes(right);
            return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
    }
}