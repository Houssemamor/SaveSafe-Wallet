namespace WalletService.API.DTOs;

/// <summary>Single ledger entry in wallet history.</summary>
public record LedgerHistoryItemDto(
    Guid Id,
    string Type, // Credit or Debit
    decimal Amount,
    decimal BalanceAfter,
    string? Description,
    DateTime CreatedAt
);

/// <summary>Paginated wallet history response.</summary>
public record WalletHistoryResponseDto(
    IEnumerable<LedgerHistoryItemDto> Entries,
    int Page,
    int PageSize,
    int TotalCount
);
