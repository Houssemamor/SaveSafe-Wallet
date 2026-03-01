using System.ComponentModel.DataAnnotations;

namespace AuthService.API.DTOs;

public record LoginRequestDto(
    [Required, EmailAddress] string Email,
    [Required] string Password
);
