using Google.Cloud.Firestore;

namespace AuthService.API.Persistence.Firestore.Documents;

[FirestoreData]
public sealed class UserMfaQuestionDocument
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [FirestoreProperty("questionId")]
    public string QuestionId { get; set; } = string.Empty;

    [FirestoreProperty("encryptedAnswer")]
    public string EncryptedAnswer { get; set; } = string.Empty;

    [FirestoreProperty("nonce")]
    public string Nonce { get; set; } = string.Empty;

    [FirestoreProperty("tag")]
    public string Tag { get; set; } = string.Empty;

    [FirestoreProperty("cipherVersion")]
    public int CipherVersion { get; set; }

    [FirestoreProperty("isActive")]
    public bool IsActive { get; set; }

    [FirestoreProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    [FirestoreProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [FirestoreProperty("lastVerifiedAt")]
    public DateTime? LastVerifiedAt { get; set; }
}