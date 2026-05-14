namespace WalletService.API.DTOs;

public sealed record InternalWithdrawResponseDto(
    bool Success,
    string? ErrorMessage,
    Guid WalletId,
    decimal NewBalance,
    string Currency,
    string? LedgerEntryId,
    bool Duplicate
);
