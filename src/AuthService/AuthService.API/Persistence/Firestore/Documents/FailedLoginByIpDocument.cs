using Google.Cloud.Firestore;

namespace AuthService.API.Persistence.Firestore.Documents;

[FirestoreData]
public sealed class FailedLoginByIpDocument
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [FirestoreProperty("failedAttempts")]
    public long FailedAttempts { get; set; }

    [FirestoreProperty("lastAttemptAt")]
    public DateTime LastAttemptAt { get; set; }
}
