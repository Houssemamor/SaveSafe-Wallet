using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WalletService.API.Entities;

[Table("accounts", Schema = "wallet")]
public class Account
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    [Column("account_number")]
    public string AccountNumber { get; set; } = default!;

    [Column("type")]
    public string? Type { get; set; } = AccountType.Savings.ToString();

    [MaxLength(3)]
    [Column("currency")]
    public string? Currency { get; set; } = "USD";

    [Column("balance", TypeName = "numeric(18,4)")]
    public decimal Balance { get; set; } = 0.00m;

    /// <summary>
    /// Wallet name for user-friendly display
    /// </summary>
    [MaxLength(50)]
    public string? Name { get; set; }

    /// <summary>
    /// Whether the wallet is active for transactions
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this is the default wallet for the user
    /// </summary>
    public bool IsDefault { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Stored as metadata in Firestore to avoid expensive ledger counts.
    public long LedgerCount { get; set; } = 0;

    public ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
}

public enum AccountType { Savings, Checking }
