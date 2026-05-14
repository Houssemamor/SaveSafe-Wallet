using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentService.API.Entities;

[Table("withdrawal_requests", Schema = "payment")]
public sealed class WithdrawalRequest
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid WalletId { get; set; }

    [Column(TypeName = "numeric(18,4)")]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [MaxLength(20)]
    public string Status { get; set; } = WithdrawalRequestStatus.Pending.ToString();

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(100)]
    public string? OperationId { get; set; }

    [MaxLength(100)]
    public string? LedgerEntryId { get; set; }

    [Column(TypeName = "numeric(18,4)")]
    public decimal? BalanceAfterDebit { get; set; }

    [MaxLength(300)]
    public string? FailureReason { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public Guid? ProcessedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}

public enum WithdrawalRequestStatus
{
    Pending,
    Approved,
    Rejected,
    Failed
}
