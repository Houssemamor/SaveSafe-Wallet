using Google.Cloud.Firestore;

namespace WalletService.API.Persistence.Firestore.Documents;

[FirestoreData]
public sealed class LedgerEntryDocument
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [FirestoreProperty("type")]
    public string Type { get; set; } = string.Empty;

    [FirestoreProperty("amount")]
    public double Amount { get; set; }

    [FirestoreProperty("balanceAfter")]
    public double BalanceAfter { get; set; }

    [FirestoreProperty("description")]
    public string? Description { get; set; }

    [FirestoreProperty("createdAt")]
    public DateTime CreatedAt { get; set; }
}
