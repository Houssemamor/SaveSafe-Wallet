namespace AuthService.API.DTOs;

public record AuthResponseDto(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    Guid UserId,
    string Email,
    string Name,
    string Role
    // Refresh token is set as httpOnly cookie, NOT included in this response body
);
