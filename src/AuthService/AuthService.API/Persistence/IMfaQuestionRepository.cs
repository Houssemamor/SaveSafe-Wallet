namespace AuthService.API.Persistence;

public sealed record UserMfaQuestionRecord(
    Guid UserId,
    string QuestionId,
    string EncryptedAnswer,
    string Nonce,
    string Tag,
    int CipherVersion,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? LastVerifiedAt);

public interface IMfaQuestionRepository
{
    Task<IReadOnlyList<UserMfaQuestionRecord>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<UserMfaQuestionRecord?> GetAsync(Guid userId, string questionId, CancellationToken ct = default);
    Task ReplaceAsync(Guid userId, IReadOnlyList<UserMfaQuestionRecord> questions, CancellationToken ct = default);
    Task DeleteByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task UpdateLastVerifiedAsync(Guid userId, string questionId, DateTime lastVerifiedAt, CancellationToken ct = default);
}