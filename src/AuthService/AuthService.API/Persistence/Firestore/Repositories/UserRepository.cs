using AuthService.API.Entities;
using AuthService.API.Persistence.Firestore.Documents;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;

namespace AuthService.API.Persistence.Firestore.Repositories;

public sealed class UserRepository : IUserRepository
{
    private const string FieldAccountStatus = "accountStatus";
    private const string FieldRole = "role";
    private const string FieldCreatedAt = "createdAt";
    private const string FieldUpdatedAt = "updatedAt";
    private const string FieldLastLoginAt = "lastLoginAt";

    private readonly IFirestoreDbProvider _dbProvider;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(IFirestoreDbProvider dbProvider, ILogger<UserRepository> logger)
    {
        _dbProvider = dbProvider;
        _logger = logger;
    }

    private FirestoreDb Db => _dbProvider.GetDb();
    private CollectionReference Users => Db.Collection(FirestoreCollections.Users);
    private CollectionReference EmailIndex => Db.Collection(FirestoreCollections.UsersByEmail);

    public async Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct = default)
    {
        var snapshot = await EmailIndex.Document(normalizedEmail).GetSnapshotAsync(ct);
        return snapshot.Exists;
    }

    public async Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default)
    {
        var snapshot = await Users.Document(userId.ToString()).GetSnapshotAsync(ct);
        if (!snapshot.Exists)
        {
            return null;
        }

        var doc = snapshot.ConvertTo<UserDocument>();
        doc.Id = snapshot.Id;
        return UserDocumentMapper.ToEntity(doc);
    }

    public async Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        var emailSnapshot = await EmailIndex.Document(normalizedEmail).GetSnapshotAsync(ct);
        if (!emailSnapshot.Exists)
        {
            return null;
        }

        var index = emailSnapshot.ConvertTo<UserEmailIndexDocument>();
        var userSnapshot = await Users.Document(index.UserId).GetSnapshotAsync(ct);
        if (!userSnapshot.Exists)
        {
            return null;
        }

        var doc = userSnapshot.ConvertTo<UserDocument>();
        doc.Id = userSnapshot.Id;
        return UserDocumentMapper.ToEntity(doc);
    }

    public async Task CreateAsync(User user, string normalizedEmail, CancellationToken ct = default)
    {
        var userDoc = Users.Document(user.Id.ToString());
        var emailDoc = EmailIndex.Document(normalizedEmail);
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
        }, null, ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        await Users.Document(user.Id.ToString())
            .SetAsync(UserDocumentMapper.ToDocument(user), SetOptions.Overwrite, ct);
    }

    public async Task UpdateLastLoginAsync(Guid userId, DateTime lastLoginAt, CancellationToken ct = default)
    {
        var updates = new Dictionary<string, object>
        {
            [FieldLastLoginAt] = lastLoginAt,
            [FieldUpdatedAt] = lastLoginAt
        };

        await Users.Document(userId.ToString()).UpdateAsync(updates, null, ct);
    }

    public async Task<IReadOnlyList<User>> GetRecentUsersAsync(int limit, CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);

        _logger.LogInformation("Querying Firestore for recent users with limit: {Limit}", safeLimit);

        try
        {
            var snapshot = await Users.OrderByDescending(FieldCreatedAt)
                .Limit(safeLimit)
                .GetSnapshotAsync(ct);

            _logger.LogInformation("Firestore query returned {Count} documents", snapshot.Count);

            if (snapshot.Count == 0)
            {
                _logger.LogWarning("No users found in Firestore collection");
            }

            var users = snapshot.Documents
                .Select(doc =>
                {
                    var userDoc = doc.ConvertTo<UserDocument>();
                    userDoc.Id = doc.Id;
                    var user = UserDocumentMapper.ToEntity(userDoc);
                    _logger.LogDebug("Converted user: {UserId}, {Email}, {Role}, {Status}",
                        user.Id, user.Email, user.Role, user.AccountStatus);
                    return user;
                })
                .ToList();

            _logger.LogInformation("Converted {Count} documents to User entities", users.Count);

            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying users from Firestore");
            throw;
        }
    }

    public async Task<UserCounts> GetUserCountsAsync(CancellationToken ct = default)
    {
        var total = await CountAsync(Users, ct);
        var active = await CountAsync(Users.WhereEqualTo(FieldAccountStatus, UserAccountStatus.Active.ToString()), ct);
        var suspended = await CountAsync(Users.WhereEqualTo(FieldAccountStatus, UserAccountStatus.Suspended.ToString()), ct);
        var deleted = await CountAsync(Users.WhereEqualTo(FieldAccountStatus, UserAccountStatus.Deleted.ToString()), ct);

        return new UserCounts(total, active, suspended, deleted);
    }

    public async Task<bool> AnyWithRoleAsync(UserRole role, CancellationToken ct = default)
    {
        var snapshot = await Users.WhereEqualTo(FieldRole, role.ToString())
            .Limit(1)
            .GetSnapshotAsync(ct);
        return snapshot.Count > 0;
    }

    private static async Task<int> CountAsync(Query query, CancellationToken ct)
    {
        var snapshot = await query.Count().GetSnapshotAsync(ct);
        return (int)snapshot.Count;
    }
}
