using System.ComponentModel.DataAnnotations;

namespace AuthService.API.DTOs;

public record RegisterRequestDto(
    [Required, EmailAddress, MaxLength(255)] string Email,
    [Required, MinLength(2), MaxLength(200)] string Name,
    [Required, MinLength(8), MaxLength(100)] string Password
);
