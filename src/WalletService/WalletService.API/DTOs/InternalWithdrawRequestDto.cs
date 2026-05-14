namespace WalletService.API.DTOs;

public sealed record InternalWithdrawRequestDto(
    Guid UserId,
    Guid WalletId,
    decimal Amount,
    string Currency,
    string ReferenceId,
    string? Notes
);
