namespace AuthService.API.DTOs;

public sealed record SecurityQuestionCatalogDto(
    string QuestionId,
    string QuestionText);

public sealed record MfaEnrollQuestionDto(
    string QuestionId,
    string Answer);

public sealed record MfaEnrollRequestDto(
    IReadOnlyList<MfaEnrollQuestionDto> Questions);

public sealed record MfaEnrollResponseDto(
    bool Success,
    bool MfaEnabled,
    int QuestionCount);

public sealed record MfaDisableResponseDto(
    bool Success,
    bool MfaEnabled);

public sealed record MfaVerifyRequestDto(
    string ChallengeToken,
    string Answer);