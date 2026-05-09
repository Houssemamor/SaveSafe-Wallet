using AuthService.API.Persistence.Firestore.Documents;
using Google.Cloud.Firestore;

namespace AuthService.API.Persistence.Firestore.Repositories;

public sealed class FailedLoginByIpRepository : IFailedLoginByIpRepository
{
    private const string FieldFailedAttempts = "failedAttempts";
    private const string FieldLastAttemptAt = "lastAttemptAt";

    private readonly IFirestoreDbProvider _dbProvider;

    public FailedLoginByIpRepository(IFirestoreDbProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    private FirestoreDb Db => _dbProvider.GetDb();
    private CollectionReference FailedLogins => Db.Collection(FirestoreCollections.FailedLoginsByIp);

    public async Task IncrementAsync(string ipAddress, DateTime timestamp, CancellationToken ct = default)
    {
        var docId = NormalizeIp(ipAddress);
        var docRef = FailedLogins.Document(docId);

        await Db.RunTransactionAsync(async transaction =>
        {
            var snapshot = await transaction.GetSnapshotAsync(docRef, ct);
            if (!snapshot.Exists)
            {
                transaction.Create(docRef, new FailedLoginByIpDocument
                {
                    Id = docId,
                    IpAddress = docId,
                    FailedAttempts = 1,
                    LastAttemptAt = timestamp
                });
                return;
            }

            var existing = snapshot.ConvertTo<FailedLoginByIpDocument>();
            var newCount = existing.FailedAttempts + 1;

            transaction.Update(docRef, new Dictionary<string, object>
            {
                [FieldFailedAttempts] = newCount,
                [FieldLastAttemptAt] = timestamp
            });
        }, null, ct);
    }

    public async Task<IReadOnlyList<FailedLoginByIpRecord>> GetTopAsync(int top, CancellationToken ct = default)
    {
        var safeTop = Math.Clamp(top, 1, 100);
        var snapshot = await FailedLogins
            .OrderByDescending(FieldFailedAttempts)
            .Limit(safeTop)
            .GetSnapshotAsync(ct);

        return snapshot.Documents
            .Select(doc =>
            {
                var record = doc.ConvertTo<FailedLoginByIpDocument>();
                record.Id = doc.Id;
                return new FailedLoginByIpRecord(
                    record.IpAddress,
                    (int)record.FailedAttempts,
                    record.LastAttemptAt);
            })
            .ToList();
    }

    private static string NormalizeIp(string ipAddress) =>
        string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress.Replace("/", "_");
}
