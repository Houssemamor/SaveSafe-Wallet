using System.ComponentModel.DataAnnotations;

namespace WalletService.API.DTOs;

public record CreateWalletRequestDto(
    [Required] Guid UserId,
    [MaxLength(3)] string Currency = "USD"
);
