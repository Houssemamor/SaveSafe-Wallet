using WalletService.API.Entities;

namespace WalletService.API.Persistence;

public sealed record LedgerPage(
    IReadOnlyList<LedgerEntry> Entries,
    string? NextPageToken);

public interface ILedgerRepository
{
    Task<LedgerPage> GetPageAsync(
        Guid accountId,
        int pageSize,
        string? pageToken,
        CancellationToken ct = default);

    Task<IReadOnlyList<LedgerEntry>> GetAllByAccountIdAsync(
        Guid accountId,
        CancellationToken ct = default);

    Task CreateAsync(LedgerEntry entry, CancellationToken ct = default);
}
