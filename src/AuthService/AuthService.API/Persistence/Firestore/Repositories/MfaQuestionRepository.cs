using AuthService.API.Persistence.Firestore.Documents;
using Google.Cloud.Firestore;

namespace AuthService.API.Persistence.Firestore.Repositories;

public sealed class MfaQuestionRepository : IMfaQuestionRepository
{
    private const string FieldUserId = "userId";
    private const string FieldIsActive = "isActive";
    private const string FieldLastVerifiedAt = "lastVerifiedAt";
    private const string FieldUpdatedAt = "updatedAt";

    private readonly IFirestoreDbProvider _dbProvider;

    public MfaQuestionRepository(IFirestoreDbProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    private FirestoreDb Db => _dbProvider.GetDb();
    private CollectionReference Questions => Db.Collection(FirestoreCollections.UserSecurityQuestions);

    public async Task<IReadOnlyList<UserMfaQuestionRecord>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var snapshot = await Questions.WhereEqualTo(FieldUserId, userId.ToString())
            .WhereEqualTo(FieldIsActive, true)
            .GetSnapshotAsync(ct);

        return snapshot.Documents.Select(ToRecord).ToList();
    }

    public async Task<UserMfaQuestionRecord?> GetAsync(Guid userId, string questionId, CancellationToken ct = default)
    {
        var docId = BuildDocumentId(userId, questionId);
        var snapshot = await Questions.Document(docId).GetSnapshotAsync(ct);
        return snapshot.Exists ? ToRecord(snapshot) : null;
    }

    public async Task ReplaceAsync(Guid userId, IReadOnlyList<UserMfaQuestionRecord> questions, CancellationToken ct = default)
    {
        var existing = await Questions.WhereEqualTo(FieldUserId, userId.ToString()).GetSnapshotAsync(ct);
        var batch = Db.StartBatch();

        foreach (var doc in existing.Documents)
        {
            batch.Delete(doc.Reference);
        }

        foreach (var question in questions)
        {
            var doc = Questions.Document(BuildDocumentId(userId, question.QuestionId));
            batch.Set(doc, ToDocument(question));
        }

        await batch.CommitAsync(ct);
    }

    public async Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var existing = await Questions.WhereEqualTo(FieldUserId, userId.ToString()).GetSnapshotAsync(ct);
        if (existing.Count == 0)
        {
            return;
        }

        var batch = Db.StartBatch();
        foreach (var doc in existing.Documents)
        {
            batch.Delete(doc.Reference);
        }

        await batch.CommitAsync(ct);
    }

    public async Task UpdateLastVerifiedAsync(Guid userId, string questionId, DateTime lastVerifiedAt, CancellationToken ct = default)
    {
        var docId = BuildDocumentId(userId, questionId);
        await Questions.Document(docId).UpdateAsync(new Dictionary<string, object>
        {
            [FieldLastVerifiedAt] = lastVerifiedAt,
            [FieldUpdatedAt] = lastVerifiedAt
        }, null, ct);
    }

    private static string BuildDocumentId(Guid userId, string questionId) => $"{userId:N}_{questionId}";

    private static UserMfaQuestionRecord ToRecord(DocumentSnapshot snapshot)
    {
        var doc = snapshot.ConvertTo<UserMfaQuestionDocument>();
        doc.Id = snapshot.Id;

        return new UserMfaQuestionRecord(
            UserId: Guid.Parse(doc.UserId),
            QuestionId: doc.QuestionId,
            EncryptedAnswer: doc.EncryptedAnswer,
            Nonce: doc.Nonce,
            Tag: doc.Tag,
            CipherVersion: doc.CipherVersion,
            IsActive: doc.IsActive,
            CreatedAt: doc.CreatedAt,
            UpdatedAt: doc.UpdatedAt,
            LastVerifiedAt: doc.LastVerifiedAt);
    }

    private static UserMfaQuestionDocument ToDocument(UserMfaQuestionRecord record) => new()
    {
        Id = BuildDocumentId(record.UserId, record.QuestionId),
        UserId = record.UserId.ToString(),
        QuestionId = record.QuestionId,
        EncryptedAnswer = record.EncryptedAnswer,
        Nonce = record.Nonce,
        Tag = record.Tag,
        CipherVersion = record.CipherVersion,
        IsActive = record.IsActive,
        CreatedAt = record.CreatedAt,
        UpdatedAt = record.UpdatedAt,
        LastVerifiedAt = record.LastVerifiedAt
    };
}