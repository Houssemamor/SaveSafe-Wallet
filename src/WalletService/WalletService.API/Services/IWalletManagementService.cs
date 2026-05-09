using WalletService.API.DTOs;

namespace WalletService.API.Services;

/// <summary>
/// Interface for wallet management operations
/// Handles creation, deletion, and management of user wallets
/// </summary>
public interface IWalletManagementService
{
    /// <summary>
    /// Get all wallets for the authenticated user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Collection of user wallets</returns>
    Task<IEnumerable<WalletResponseDto>> GetUserWalletsAsync(string userId);

    /// <summary>
    /// Create a new wallet for the user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="request">Wallet creation request</param>
    /// <returns>Created wallet response</returns>
    Task<CreateWalletResponseDto> CreateWalletAsync(string userId, CreateManagedWalletRequestDto request);

    /// <summary>
    /// Delete a wallet by ID
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="walletId">Wallet identifier</param>
    /// <returns>Task representing the async operation</returns>
    Task DeleteWalletAsync(string userId, string walletId);

    /// <summary>
    /// Set a wallet as the default wallet for the user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="walletId">Wallet identifier</param>
    /// <returns>Task representing the async operation</returns>
    Task SetDefaultWalletAsync(string userId, string walletId);

    /// <summary>
    /// Get balance for a specific wallet
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="walletId">Wallet identifier</param>
    /// <returns>Wallet balance response</returns>
    Task<WalletBalanceResponseDto> GetWalletBalanceAsync(string userId, string walletId);

    /// <summary>
    /// Transfer funds between user's own wallets
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="sourceWalletId">Source wallet identifier</param>
    /// <param name="targetWalletId">Target wallet identifier</param>
    /// <param name="amount">Transfer amount</param>
    /// <returns>Transfer response</returns>
    Task<InternalTransferResponseDto> TransferBetweenWalletsAsync(
        string userId,
        string sourceWalletId,
        string targetWalletId,
        decimal amount);
}