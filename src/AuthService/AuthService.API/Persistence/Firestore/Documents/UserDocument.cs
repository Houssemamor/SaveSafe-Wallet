using Google.Cloud.Firestore;

namespace AuthService.API.Persistence.Firestore.Documents;

[FirestoreData]
public sealed class UserDocument
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("email")]
    public string Email { get; set; } = string.Empty;

    [FirestoreProperty("name")]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [FirestoreProperty("googleId")]
    public string? GoogleId { get; set; }

    [FirestoreProperty("mfaEnabled")]
    public bool MfaEnabled { get; set; }

    [FirestoreProperty("accountStatus")]
    public string AccountStatus { get; set; } = string.Empty;

    [FirestoreProperty("role")]
    public string Role { get; set; } = string.Empty;

    [FirestoreProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    [FirestoreProperty("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [FirestoreProperty("lastLoginAt")]
    public DateTime? LastLoginAt { get; set; }
}
