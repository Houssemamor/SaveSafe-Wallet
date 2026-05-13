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
    DateTime? LastLoginAt,
    string? ProfilePictureUrl = null,
    bool HasPassword = false,
    bool IsGoogleAccount = false
);

/// <summary>Request body for PUT /api/users/profile.</summary>
public record UpdateProfileRequestDto(
    string Name
);

/// <summary>Request body for PUT /api/users/profile/password.</summary>
public record UpdatePasswordRequestDto(
    string NewPassword,
    string? CurrentPassword = null
);
