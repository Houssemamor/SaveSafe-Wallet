namespace PaymentService.API.DTOs;

public sealed record CreateCheckoutSessionRequestDto(
    decimal Amount
);

public sealed record CreateCheckoutSessionResponseDto(
    bool Success,
    string? SessionId,
    string? SessionUrl,
    Guid? TransactionId,
    Guid? WalletId,
    string? Currency,
    string? ErrorMessage
);

public sealed record WalletSummaryDto(
    string Id,
    string Name,
    string Type,
    decimal Balance,
    string Currency,
    bool IsActive,
    DateTime CreatedAt,
    bool IsDefault
);

public sealed record WalletBalanceDto(
    Guid AccountId,
    string AccountNumber,
    string Currency,
    decimal Balance,
    DateTime UpdatedAt
);

public sealed record InternalTopUpRequestDto(
    Guid UserId,
    Guid WalletId,
    decimal Amount,
    string Currency,
    string PaymentIntentId,
    string StripeEventId,
    string StripeSessionId
);

public sealed record InternalTopUpResponseDto(
    bool Success,
    string? ErrorMessage,
    Guid WalletId,
    decimal NewBalance,
    string Currency,
    string? LedgerEntryId,
    bool Duplicate
);

public sealed record StripeWebhookResponseDto(
    bool Success,
    string? ErrorMessage
);
