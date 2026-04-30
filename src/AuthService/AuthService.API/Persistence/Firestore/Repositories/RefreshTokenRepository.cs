using AuthService.API.Persistence.Firestore.Documents;
using Google.Cloud.Firestore;

namespace AuthService.API.Persistence.Firestore.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private const string FieldUserId = "userId";
    private const string FieldIsRevoked = "isRevoked";

    private readonly IFirestoreDbProvider _dbProvider;

    public RefreshTokenRepository(IFirestoreDbProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    private FirestoreDb Db => _dbProvider.GetDb();
    private CollectionReference Tokens => Db.Collection(FirestoreCollections.RefreshTokens);

    public async Task CreateAsync(RefreshTokenRecord token, CancellationToken ct = default)
    {
        await Tokens.Document(token.TokenHash)
            .SetAsync(ToDocument(token), SetOptions.Overwrite, ct);
    }

    public async Task<Guid> RotateAsync(string currentTokenHash, RefreshTokenRecord newToken, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var tokenDoc = Tokens.Document(currentTokenHash);

        return await Db.RunTransactionAsync(async transaction =>
        {
            var snapshot = await transaction.GetSnapshotAsync(tokenDoc, ct);
            if (!snapshot.Exists)
            {
                throw new UnauthorizedAccessException("Invalid or expired refresh token.");
            }

            var existing = snapshot.ConvertTo<RefreshTokenDocument>();
            existing.Id = snapshot.Id;

            if (existing.IsRevoked || existing.ExpiresAt <= now)
            {
                throw new UnauthorizedAccessException("Invalid or expired refresh token.");
            }

            var userId = Guid.Parse(existing.UserId);

            transaction.Update(tokenDoc, new Dictionary<string, object>
            {
                [FieldIsRevoked] = true
            });

            var newTokenDoc = Tokens.Document(newToken.TokenHash);
            transaction.Create(newTokenDoc, ToDocument(newToken with { UserId = userId }));

            return userId;
        }, ct);
    }

    public async Task RevokeAsync(string tokenHash, CancellationToken ct = default)
    {
        var docRef = Tokens.Document(tokenHash);
        var snapshot = await docRef.GetSnapshotAsync(ct);
        if (!snapshot.Exists)
        {
            return;
        }

        await docRef.UpdateAsync(new Dictionary<string, object>
        {
            [FieldIsRevoked] = true
        }, ct);
    }

    public async Task RevokeAllActiveForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var query = Tokens
            .WhereEqualTo(FieldUserId, userId.ToString())
            .WhereEqualTo(FieldIsRevoked, false);

        var snapshot = await query.GetSnapshotAsync(ct);
        if (snapshot.Count == 0)
        {
            return;
        }

        var batch = Db.StartBatch();
        var operations = 0;

        foreach (var doc in snapshot.Documents)
        {
            batch.Update(doc.Reference, new Dictionary<string, object>
            {
                [FieldIsRevoked] = true
            });

            operations++;
            if (operations >= 400)
            {
                await batch.CommitAsync(ct);
                batch = Db.StartBatch();
                operations = 0;
            }
        }

        if (operations > 0)
        {
            await batch.CommitAsync(ct);
        }
    }

    private static RefreshTokenDocument ToDocument(RefreshTokenRecord token) =>
        new()
        {
            Id = token.TokenHash,
            UserId = token.UserId.ToString(),
            TokenHash = token.TokenHash,
            ExpiresAt = token.ExpiresAt,
            IsRevoked = token.IsRevoked,
            CreatedAt = token.CreatedAt
        };
}
