using Google.Cloud.Firestore;

namespace AuthService.API.Persistence.Firestore.Documents;

[FirestoreData]
public sealed class UserEmailIndexDocument
{
    [FirestoreProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [FirestoreProperty("createdAt")]
    public DateTime CreatedAt { get; set; }
}
