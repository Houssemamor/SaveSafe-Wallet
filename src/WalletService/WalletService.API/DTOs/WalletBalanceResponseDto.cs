namespace WalletService.API.DTOs;

public record WalletBalanceResponseDto(
    Guid AccountId,
    string AccountNumber,
    string Currency,
    decimal Balance,
    DateTime UpdatedAt
);
