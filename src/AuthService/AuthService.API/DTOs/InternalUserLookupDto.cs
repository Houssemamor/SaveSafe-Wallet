namespace AuthService.API.DTOs;

public record InternalUserLookupDto(
    Guid? UserId,
    string? Name,
    string? Email
);