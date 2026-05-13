namespace WalletService.API.DTOs;

/// <summary>
/// Response data transfer object for wallet information
/// </summary>
public class WalletResponseDto
{
    /// <summary>
    /// Unique wallet identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Wallet name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Wallet type (checking, savings, investment, reserve)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Current wallet balance
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Wallet currency code
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Whether the wallet is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Wallet creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Whether this is the default wallet for the user
    /// </summary>
    public bool IsDefault { get; set; }
}

/// <summary>
/// Response data transfer object for wallet creation
/// </summary>
public class CreateWalletResponseDto
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Created wallet information (if successful)
    /// </summary>
    public WalletResponseDto? Wallet { get; set; }

    /// <summary>
    /// Error message (if unsuccessful)
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Response data transfer object for receive QR creation
/// </summary>
public class ReceiveWalletQrResponseDto
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Signed token embedded in the QR code
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Wallet identifier encoded in the token
    /// </summary>
    public string? WalletId { get; set; }

    /// <summary>
    /// Wallet name shown to the user
    /// </summary>
    public string? WalletName { get; set; }

    /// <summary>
    /// Wallet currency
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Token expiration timestamp
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Error message (if unsuccessful)
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request data transfer object for resolving a receive QR token
/// </summary>
public class ResolveWalletQrRequestDto
{
    /// <summary>
    /// Signed receive QR token
    /// </summary>
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// Response data transfer object for resolving a receive QR token
/// </summary>
public class ResolveWalletQrResponseDto
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Wallet identifier recovered from the token
    /// </summary>
    public string? WalletId { get; set; }

    /// <summary>
    /// Wallet name recovered from the token
    /// </summary>
    public string? WalletName { get; set; }

    /// <summary>
    /// Wallet currency recovered from the token
    /// </summary>
    public string? Currency { get; set; }

    /// <summary>
    /// Error message (if unsuccessful)
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Response data transfer object for internal wallet transfer
/// </summary>
public class InternalTransferResponseDto
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message (if unsuccessful)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Transaction ID (if successful)
    /// </summary>
    public string? TransactionId { get; set; }
}