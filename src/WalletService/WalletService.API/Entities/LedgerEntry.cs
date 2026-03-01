using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WalletService.API.Entities;

// Append-only: rows are never updated or deleted
[Table("ledger_entries", Schema = "wallet")]
public class LedgerEntry
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("account_id")]
    public Guid AccountId { get; set; }

    [Column("type")]
    public LedgerEntryType Type { get; set; }

    [Column("amount", TypeName = "numeric(18,4)")]
    public decimal Amount { get; set; }

    // Snapshot of balance after this entry - enables audit trail without re-aggregating
    [Column("balance_after", TypeName = "numeric(18,4)")]
    public decimal BalanceAfter { get; set; }

    [MaxLength(300)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("AccountId")]
    public Account Account { get; set; } = default!;
}

public enum LedgerEntryType { Credit, Debit }
