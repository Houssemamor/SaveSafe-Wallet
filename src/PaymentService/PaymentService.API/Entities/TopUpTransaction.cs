using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentService.API.Entities;

[Table("top_up_transactions", Schema = "payment")]
public sealed class TopUpTransaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid WalletId { get; set; }

    [MaxLength(100)]
    public string? StripeSessionId { get; set; }

    [MaxLength(100)]
    public string? StripePaymentIntentId { get; set; }

    [MaxLength(100)]
    public string? StripeEventId { get; set; }

    [Column(TypeName = "numeric(18,4)")]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [MaxLength(20)]
    public string Status { get; set; } = TopUpTransactionStatus.Pending.ToString();

    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    [MaxLength(300)]
    public string? FailureReason { get; set; }
}

public enum TopUpTransactionStatus
{
    Pending,
    Succeeded,
    Failed,
    Cancelled
}
