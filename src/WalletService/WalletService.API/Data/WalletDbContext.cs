using Microsoft.EntityFrameworkCore;
using WalletService.API.Entities;

namespace WalletService.API.Data;

public class WalletDbContext : DbContext
{
    public WalletDbContext(DbContextOptions<WalletDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("wallet");

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasIndex(a => a.UserId).IsUnique();           // One account per user
            entity.HasIndex(a => a.AccountNumber).IsUnique();
            entity.Property(a => a.Type).HasConversion<string>();
        });

        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.HasIndex(le => le.AccountId);
            entity.HasIndex(le => le.CreatedAt);
            entity.Property(le => le.Type).HasConversion<string>();
        });
    }
}
