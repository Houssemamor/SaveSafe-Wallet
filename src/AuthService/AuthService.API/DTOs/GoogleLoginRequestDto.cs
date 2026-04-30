using System.ComponentModel.DataAnnotations;

namespace AuthService.API.DTOs;

public record GoogleLoginRequestDto(
    [Required] string IdToken
);