using Google.Cloud.Firestore;

namespace AuthService.API.Persistence.Firestore.Documents;

[FirestoreData]
public sealed class RefreshTokenDocument
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [FirestoreProperty("tokenHash")]
    public string TokenHash { get; set; } = string.Empty;

    [FirestoreProperty("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [FirestoreProperty("isRevoked")]
    public bool IsRevoked { get; set; }

    [FirestoreProperty("createdAt")]
    public DateTime CreatedAt { get; set; }
}
