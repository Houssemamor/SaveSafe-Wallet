namespace WalletService.API.DTOs;

public record WalletTransferRequestDto
{
    public string RecipientEmail { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Description { get; init; }
}

public record WalletTransferResponseDto
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public Guid? TransactionId { get; init; }
    public decimal? NewBalance { get; init; }
    public string? RecipientName { get; init; }
}