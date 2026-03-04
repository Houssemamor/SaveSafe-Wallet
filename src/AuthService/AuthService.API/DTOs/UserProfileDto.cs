namespace AuthService.API.DTOs;

/// <summary>User profile data for GET /api/users/profile.</summary>
public record UserProfileDto(
    Guid UserId,
    string Email,
    string Name,
    bool MfaEnabled,
    string AccountStatus,
    string Role,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

/// <summary>Request body for PUT /api/users/profile.</summary>
public record UpdateProfileRequestDto(
    string Name
);
