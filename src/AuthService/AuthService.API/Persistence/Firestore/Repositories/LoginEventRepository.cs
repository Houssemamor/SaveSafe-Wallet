using AuthService.API.Persistence.Firestore.Documents;
using Google.Cloud.Firestore;

namespace AuthService.API.Persistence.Firestore.Repositories;

public sealed class LoginEventRepository : ILoginEventRepository
{
    private const string FieldTimestamp = "timestamp";

    private readonly IFirestoreDbProvider _dbProvider;

    public LoginEventRepository(IFirestoreDbProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    private FirestoreDb Db => _dbProvider.GetDb();
    private CollectionReference Events => Db.Collection(FirestoreCollections.LoginEvents);

    public async Task AddAsync(LoginEventRecord record, CancellationToken ct = default)
    {
        var docRef = Events.Document(record.EventId.ToString());
        await docRef.SetAsync(ToDocument(record), SetOptions.Overwrite, ct);
    }

    public async Task<IReadOnlyList<LoginEventRecord>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);
        var snapshot = await Events
            .OrderByDescending(FieldTimestamp)
            .Limit(safeLimit)
            .GetSnapshotAsync(ct);

        return snapshot.Documents
            .Select(doc => ToRecord(doc))
            .ToList();
    }

    public async Task<IReadOnlyList<LoginEventRecord>> GetEventsSinceAsync(DateTime sinceUtc, CancellationToken ct = default)
    {
        var snapshot = await Events
            .WhereGreaterThanOrEqualTo(FieldTimestamp, sinceUtc)
            .GetSnapshotAsync(ct);

        return snapshot.Documents
            .Select(doc => ToRecord(doc))
            .ToList();
    }

    private static LoginEventRecord ToRecord(DocumentSnapshot snapshot)
    {
        var doc = snapshot.ConvertTo<LoginEventDocument>();
        doc.Id = snapshot.Id;

        return new LoginEventRecord(
            EventId: Guid.TryParse(doc.Id, out var id) ? id : Guid.Empty,
            UserId: Guid.Parse(doc.UserId),
            UserEmail: doc.UserEmail,
            UserName: doc.UserName,
            IpAddress: doc.IpAddress,
            Country: doc.Country,
            Success: doc.Success,
            FailureReason: doc.FailureReason,
            IsFlagged: doc.IsFlagged,
            Timestamp: doc.Timestamp,
            UserAgent: doc.UserAgent);
    }

    private static LoginEventDocument ToDocument(LoginEventRecord record) =>
        new()
        {
            Id = record.EventId.ToString(),
            UserId = record.UserId.ToString(),
            UserEmail = record.UserEmail,
            UserName = record.UserName,
            IpAddress = record.IpAddress,
            Country = record.Country,
            Success = record.Success,
            FailureReason = record.FailureReason,
            IsFlagged = record.IsFlagged,
            Timestamp = record.Timestamp,
            UserAgent = record.UserAgent
        };
}
