namespace AuthService.API.DTOs;

public record AuthResponseDto(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    Guid UserId,
    string Email,
    string Name,
    string Role,
    string? ProfilePictureUrl = null,
    bool MfaRequired = false,
    string? MfaChallengeToken = null,
    string? MfaQuestionId = null,
    string? MfaQuestionText = null,
    DateTime? MfaExpiresAt = null
    // Refresh token is set as httpOnly cookie, NOT included in this response body
);
