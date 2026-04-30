using Google.Cloud.Firestore;

namespace WalletService.API.Persistence.Firestore.Documents;

[FirestoreData]
public sealed class AccountUserIndexDocument
{
    [FirestoreProperty("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [FirestoreProperty("createdAt")]
    public DateTime CreatedAt { get; set; }
}
