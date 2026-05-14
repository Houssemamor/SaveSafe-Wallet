namespace PaymentService.API.DTOs;

public sealed record CreateCheckoutSessionRequestDto(
    decimal Amount,
    string? ReturnBaseUrl
);

public sealed record CreateWithdrawRequestDto(
    decimal Amount,
    string? Notes
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

public sealed record CreateWithdrawResponseDto(
    bool Success,
    Guid? WithdrawalRequestId,
    string? Status,
    decimal? NewBalance,
    string? Currency,
    string? ErrorMessage
);

public sealed record WithdrawalRequestDto(
    Guid Id,
    Guid UserId,
    Guid WalletId,
    decimal Amount,
    string Currency,
    string Status,
    string? Notes,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    Guid? ProcessedBy,
    string? RejectionReason
);

public sealed record RejectWithdrawalRequestDto(
    string? RejectionReason
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

public sealed record InternalWithdrawRequestDto(
    Guid UserId,
    Guid WalletId,
    decimal Amount,
    string Currency,
    string ReferenceId,
    string? Notes
);

public sealed record InternalWithdrawResponseDto(
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
