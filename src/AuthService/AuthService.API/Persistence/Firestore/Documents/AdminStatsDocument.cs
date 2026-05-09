using Google.Cloud.Firestore;

namespace AuthService.API.Persistence.Firestore.Documents;

[FirestoreData]
public sealed class AdminStatsDocument
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("totalUsers")]
    public int TotalUsers { get; set; }

    [FirestoreProperty("activeUsers")]
    public int ActiveUsers { get; set; }

    [FirestoreProperty("suspendedUsers")]
    public int SuspendedUsers { get; set; }

    [FirestoreProperty("deletedUsers")]
    public int DeletedUsers { get; set; }

    [FirestoreProperty("totalLoginEventsLast24Hours")]
    public int TotalLoginEventsLast24Hours { get; set; }

    [FirestoreProperty("failedLoginEventsLast24Hours")]
    public int FailedLoginEventsLast24Hours { get; set; }

    [FirestoreProperty("flaggedEventsLast24Hours")]
    public int FlaggedEventsLast24Hours { get; set; }

    [FirestoreProperty("distinctSourceIpsLast24Hours")]
    public int DistinctSourceIpsLast24Hours { get; set; }

    [FirestoreProperty("computedAt")]
    public DateTime ComputedAt { get; set; }
}
