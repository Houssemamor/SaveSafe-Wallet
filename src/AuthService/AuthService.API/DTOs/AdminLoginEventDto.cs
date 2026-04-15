namespace AuthService.API.DTOs;

public record AdminLoginEventDto(
    Guid EventId,
    Guid UserId,
    string UserEmail,
    string UserName,
    string? IpAddress,
    string? Country,
    bool Success,
    string? FailureReason,
    bool IsFlagged,
    DateTime Timestamp
);

public record AdminFailedLoginByIpDto(
    string IpAddress,
    int FailedAttempts,
    DateTime LastAttemptAt
);
