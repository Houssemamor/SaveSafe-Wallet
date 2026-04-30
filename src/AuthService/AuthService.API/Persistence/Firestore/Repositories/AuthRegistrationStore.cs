using AuthService.API.Entities;
using AuthService.API.Persistence.Firestore.Documents;
using Google.Cloud.Firestore;

namespace AuthService.API.Persistence.Firestore.Repositories;

public sealed class AuthRegistrationStore : IAuthRegistrationStore
{
    private readonly IFirestoreDbProvider _dbProvider;

    public AuthRegistrationStore(IFirestoreDbProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    private FirestoreDb Db => _dbProvider.GetDb();

    public async Task RegisterAsync(
        User user,
        string normalizedEmail,
        RefreshTokenRecord refreshToken,
        CancellationToken ct = default)
    {
        var users = Db.Collection(FirestoreCollections.Users);
        var emails = Db.Collection(FirestoreCollections.UsersByEmail);
        var tokens = Db.Collection(FirestoreCollections.RefreshTokens);

        var userDoc = users.Document(user.Id.ToString());
        var emailDoc = emails.Document(normalizedEmail);
        var tokenDoc = tokens.Document(refreshToken.TokenHash);

        var emailIndex = new UserEmailIndexDocument
        {
            UserId = user.Id.ToString(),
            CreatedAt = user.CreatedAt
        };

        await Db.RunTransactionAsync(async transaction =>
        {
            var emailSnapshot = await transaction.GetSnapshotAsync(emailDoc, ct);
            if (emailSnapshot.Exists)
            {
                throw new InvalidOperationException("Email is already registered.");
            }

            transaction.Create(userDoc, UserDocumentMapper.ToDocument(user));
            transaction.Create(emailDoc, emailIndex);
            transaction.Create(tokenDoc, ToDocument(refreshToken));
        }, ct);
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
