namespace WalletService.API.DTOs;

public sealed record InternalTopUpRequestDto(
    Guid UserId,
    Guid WalletId,
    decimal Amount,
    string Currency,
    string PaymentIntentId,
    string StripeEventId,
    string StripeSessionId
);
