namespace AuthService.API.DTOs;

public sealed record ForgotInitiateRequestDto(string Email);

public sealed record ForgotInitiateResponseDto(bool Success, string? QuestionText, string? ChallengeToken);

public sealed record ForgotVerifyRequestDto(string ChallengeToken, string Answer);

public sealed record ForgotVerifyResponseDto(bool Success, string? PasswordResetToken);

public sealed record ResetPasswordRequestDto(string PasswordResetToken, string NewPassword);

public sealed record ResetPasswordResponseDto(bool Success);
