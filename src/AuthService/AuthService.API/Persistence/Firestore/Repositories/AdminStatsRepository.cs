using AuthService.API.Persistence.Firestore.Documents;
using Google.Cloud.Firestore;

namespace AuthService.API.Persistence.Firestore.Repositories;

public sealed class AdminStatsRepository : IAdminStatsRepository
{
    private const string DocId = "current";

    private readonly IFirestoreDbProvider _dbProvider;

    public AdminStatsRepository(IFirestoreDbProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    private FirestoreDb Db => _dbProvider.GetDb();
    private DocumentReference StatsDoc => Db.Collection(FirestoreCollections.AdminStats).Document(DocId);

    public async Task<AdminStatsSnapshot?> GetCurrentAsync(CancellationToken ct = default)
    {
        var snapshot = await StatsDoc.GetSnapshotAsync(ct);
        if (!snapshot.Exists)
        {
            return null;
        }

        var doc = snapshot.ConvertTo<AdminStatsDocument>();
        return new AdminStatsSnapshot(
            doc.TotalUsers,
            doc.ActiveUsers,
            doc.SuspendedUsers,
            doc.DeletedUsers,
            doc.TotalLoginEventsLast24Hours,
            doc.FailedLoginEventsLast24Hours,
            doc.FlaggedEventsLast24Hours,
            doc.DistinctSourceIpsLast24Hours,
            doc.ComputedAt);
    }

    public async Task UpsertAsync(AdminStatsSnapshot snapshot, CancellationToken ct = default)
    {
        var doc = new AdminStatsDocument
        {
            Id = DocId,
            TotalUsers = snapshot.TotalUsers,
            ActiveUsers = snapshot.ActiveUsers,
            SuspendedUsers = snapshot.SuspendedUsers,
            DeletedUsers = snapshot.DeletedUsers,
            TotalLoginEventsLast24Hours = snapshot.TotalLoginEventsLast24Hours,
            FailedLoginEventsLast24Hours = snapshot.FailedLoginEventsLast24Hours,
            FlaggedEventsLast24Hours = snapshot.FlaggedEventsLast24Hours,
            DistinctSourceIpsLast24Hours = snapshot.DistinctSourceIpsLast24Hours,
            ComputedAt = snapshot.ComputedAt
        };

        await StatsDoc.SetAsync(doc, SetOptions.Overwrite, ct);
    }
}
