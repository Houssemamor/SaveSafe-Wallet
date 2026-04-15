namespace AuthService.API.DTOs;

public record AdminUserDto(
    Guid UserId,
    string Email,
    string Name,
    string Role,
    string AccountStatus,
    bool MfaEnabled,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);
