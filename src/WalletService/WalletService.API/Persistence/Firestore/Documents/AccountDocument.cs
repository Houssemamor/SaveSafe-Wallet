using Google.Cloud.Firestore;

namespace WalletService.API.Persistence.Firestore.Documents;

[FirestoreData]
public sealed class AccountDocument
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [FirestoreProperty("accountNumber")]
    public string AccountNumber { get; set; } = string.Empty;

    [FirestoreProperty("type")]
    public string Type { get; set; } = string.Empty;

    [FirestoreProperty("currency")]
    public string Currency { get; set; } = string.Empty;

    [FirestoreProperty("balance")]
    public double Balance { get; set; }

    /// <summary>
    /// Wallet name for user-friendly display
    /// </summary>
    [FirestoreProperty("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Whether the wallet is active for transactions
    /// </summary>
    [FirestoreProperty("isActive")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this is the default wallet for the user
    /// </summary>
    [FirestoreProperty("isDefault")]
    public bool IsDefault { get; set; } = false;

    [FirestoreProperty("ledgerCount")]
    public long LedgerCount { get; set; }

    [FirestoreProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    [FirestoreProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
