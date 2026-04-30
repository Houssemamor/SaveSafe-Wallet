using Google.Cloud.Firestore;

namespace AuthService.API.Persistence.Firestore.Documents;

[FirestoreData]
public sealed class LoginEventDocument
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [FirestoreProperty("userEmail")]
    public string UserEmail { get; set; } = string.Empty;

    [FirestoreProperty("userName")]
    public string UserName { get; set; } = string.Empty;

    [FirestoreProperty("ipAddress")]
    public string? IpAddress { get; set; }

    [FirestoreProperty("country")]
    public string? Country { get; set; }

    [FirestoreProperty("success")]
    public bool Success { get; set; }

    [FirestoreProperty("failureReason")]
    public string? FailureReason { get; set; }

    [FirestoreProperty("isFlagged")]
    public bool IsFlagged { get; set; }

    [FirestoreProperty("timestamp")]
    public DateTime Timestamp { get; set; }

    [FirestoreProperty("userAgent")]
    public string? UserAgent { get; set; }
}
