using Google.Cloud.Firestore;
using WalletService.API.Entities;
using WalletService.API.Persistence.Firestore.Documents;

namespace WalletService.API.Persistence.Firestore.Repositories;

public sealed class LedgerRepository : ILedgerRepository
{
    private const string FieldCreatedAt = "createdAt";

    private readonly IFirestoreDbProvider _dbProvider;

    public LedgerRepository(IFirestoreDbProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    private FirestoreDb Db => _dbProvider.GetDb();

    public async Task<LedgerPage> GetPageAsync(
        Guid accountId,
        int pageSize,
        string? pageToken,
        CancellationToken ct = default)
    {
        var collection = Db.Collection(FirestoreCollections.Accounts)
            .Document(accountId.ToString())
            .Collection(FirestoreCollections.LedgerEntries);

        var query = collection
            .OrderByDescending(FieldCreatedAt)
            .Limit(pageSize);

        if (LedgerPageTokenCodec.TryDecode(pageToken, out var token))
        {
            query = query.StartAfter(token.CreatedAt, token.Id);
        }

        var snapshot = await query.GetSnapshotAsync(ct);
        var entries = snapshot.Documents
            .Select(ToEntity)
            .ToList();

        var nextPageToken = BuildNextPageToken(snapshot);
        return new LedgerPage(entries, nextPageToken);
    }

    private static LedgerEntry ToEntity(DocumentSnapshot snapshot)
    {
        var doc = snapshot.ConvertTo<LedgerEntryDocument>();
        doc.Id = snapshot.Id;

        return new LedgerEntry
        {
            Id = Guid.Parse(doc.Id),
            AccountId = Guid.Parse(doc.AccountId),
            Type = Enum.Parse<LedgerEntryType>(doc.Type),
            Amount = ToDecimal(doc.Amount),
            BalanceAfter = ToDecimal(doc.BalanceAfter),
            Description = doc.Description,
            CreatedAt = doc.CreatedAt
        };
    }

    private static string? BuildNextPageToken(QuerySnapshot snapshot)
    {
        if (snapshot.Count == 0)
        {
            return null;
        }

        var last = snapshot.Documents.Last();
        var lastDoc = last.ConvertTo<LedgerEntryDocument>();
        var token = new LedgerPageToken(lastDoc.CreatedAt, last.Id);
        return LedgerPageTokenCodec.Encode(token);
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetAllByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        var collection = Db.Collection(FirestoreCollections.Accounts)
            .Document(accountId.ToString())
            .Collection(FirestoreCollections.LedgerEntries);

        var snapshot = await collection
            .OrderByDescending(FieldCreatedAt)
            .GetSnapshotAsync(ct);

        return snapshot.Documents
            .Select(ToEntity)
            .ToList();
    }

    public async Task CreateAsync(LedgerEntry entry, CancellationToken ct = default)
    {
        var collection = Db.Collection(FirestoreCollections.Accounts)
            .Document(entry.AccountId.ToString())
            .Collection(FirestoreCollections.LedgerEntries);

        var doc = new LedgerEntryDocument
        {
            Id = entry.Id.ToString(),
            AccountId = entry.AccountId.ToString(),
            Type = entry.Type.ToString(),
            Amount = (double)entry.Amount,
            BalanceAfter = (double)entry.BalanceAfter,
            Description = entry.Description,
            CreatedAt = entry.CreatedAt
        };

        await collection.Document(doc.Id).SetAsync(doc);
    }

    private static decimal ToDecimal(double value) =>
        Math.Round((decimal)value, 3, MidpointRounding.AwayFromZero);
}
