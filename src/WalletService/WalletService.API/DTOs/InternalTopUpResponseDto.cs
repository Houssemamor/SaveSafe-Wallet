namespace WalletService.API.DTOs;

public sealed record InternalTopUpResponseDto(
    bool Success,
    string? ErrorMessage,
    Guid WalletId,
    decimal NewBalance,
    string Currency,
    string? LedgerEntryId,
    bool Duplicate
);
