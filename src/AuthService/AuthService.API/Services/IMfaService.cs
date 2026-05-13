using AuthService.API.DTOs;

namespace AuthService.API.Services;

public sealed record MfaChallengeResult(
    string ChallengeToken,
    string QuestionId,
    string QuestionText,
    DateTime ExpiresAt);

public interface IMfaService
{
    IReadOnlyList<SecurityQuestionCatalogDto> GetQuestionCatalog();
    Task<MfaChallengeResult> CreateChallengeAsync(Guid userId, CancellationToken ct = default);
    Task<Guid> VerifyChallengeAsync(string challengeToken, string answer, CancellationToken ct = default);
    Task EnableAsync(Guid userId, IReadOnlyList<MfaEnrollQuestionDto> questions, CancellationToken ct = default);
    Task DisableAsync(Guid userId, CancellationToken ct = default);
}