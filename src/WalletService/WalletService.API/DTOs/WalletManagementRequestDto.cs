namespace WalletService.API.DTOs;

/// <summary>
/// Request data transfer object for creating a new managed wallet with multi-wallet support
/// </summary>
public class CreateManagedWalletRequestDto
{
    /// <summary>
    /// Wallet name (2-50 characters)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Wallet type (checking, savings, investment, reserve)
    /// </summary>
    public string Type { get; set; } = "checking";

    /// <summary>
    /// Wallet currency code (e.g., USD, EUR)
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Optional initial balance for the wallet
    /// </summary>
    public decimal? InitialBalance { get; set; }
}

/// <summary>
/// Request data transfer object for setting default wallet
/// </summary>
public class SetDefaultWalletRequestDto
{
    /// <summary>
    /// Wallet ID to set as default
    /// </summary>
    public string WalletId { get; set; } = string.Empty;
}

/// <summary>
/// Request data transfer object for internal wallet transfers
/// </summary>
public class InternalTransferRequestDto
{
    /// <summary>
    /// Source wallet ID
    /// </summary>
    public string SourceWalletId { get; set; } = string.Empty;

    /// <summary>
    /// Target wallet ID
    /// </summary>
    public string TargetWalletId { get; set; } = string.Empty;

    /// <summary>
    /// Transfer amount
    /// </summary>
    public decimal Amount { get; set; }
}