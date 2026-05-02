using Google.Cloud.Firestore;
using WalletService.API.Entities;
using WalletService.API.Persistence.Firestore.Documents;

namespace WalletService.API.Persistence.Firestore.Repositories;

public sealed class AccountRepository : IAccountRepository
{
    private const string FieldCreatedAt = "createdAt";

    private readonly IFirestoreDbProvider _dbProvider;

    public AccountRepository(IFirestoreDbProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    private FirestoreDb Db => _dbProvider.GetDb();
    private CollectionReference Accounts => Db.Collection(FirestoreCollections.Accounts);
    private CollectionReference AccountsByUser => Db.Collection(FirestoreCollections.AccountsByUser);

    public async Task<Account?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var indexSnapshot = await AccountsByUser.Document(userId.ToString()).GetSnapshotAsync(ct);
        if (!indexSnapshot.Exists)
        {
            return null;
        }

        var index = indexSnapshot.ConvertTo<AccountUserIndexDocument>();
        var accountSnapshot = await Accounts.Document(index.AccountId).GetSnapshotAsync(ct);
        if (!accountSnapshot.Exists)
        {
            return null;
        }

        var doc = accountSnapshot.ConvertTo<AccountDocument>();
        doc.Id = accountSnapshot.Id;
        return ToEntity(doc);
    }

    public async Task<Account?> GetByIdAsync(Guid accountId, CancellationToken ct = default)
    {
        var snapshot = await Accounts.Document(accountId.ToString()).GetSnapshotAsync(ct);
        if (!snapshot.Exists)
        {
            return null;
        }

        var doc = snapshot.ConvertTo<AccountDocument>();
        doc.Id = snapshot.Id;
        return ToEntity(doc);
    }

    public async Task<AccountCreateResult> GetOrCreateAsync(Guid userId, string currency, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var indexDoc = AccountsByUser.Document(userId.ToString());

        return await Db.RunTransactionAsync(async transaction =>
        {
            var indexSnapshot = await transaction.GetSnapshotAsync(indexDoc, ct);
            if (indexSnapshot.Exists)
            {
                var index = indexSnapshot.ConvertTo<AccountUserIndexDocument>();
                var accountSnapshot = await transaction.GetSnapshotAsync(Accounts.Document(index.AccountId), ct);
                var existingAccountDoc = accountSnapshot.ConvertTo<AccountDocument>();
                existingAccountDoc.Id = accountSnapshot.Id;

                return new AccountCreateResult(
                    Guid.Parse(existingAccountDoc.Id),
                    existingAccountDoc.AccountNumber,
                    Created: false);
            }

            var accountId = Guid.NewGuid();
            var accountNumber = $"SSW-{accountId:N}".ToUpperInvariant();

            var accountDoc = new AccountDocument
            {
                Id = accountId.ToString(),
                UserId = userId.ToString(),
                AccountNumber = accountNumber,
                Type = AccountType.Savings.ToString(),
                Currency = currency.ToUpperInvariant(),
                Balance = 0.0d,
                LedgerCount = 1,
                CreatedAt = now,
                UpdatedAt = now
            };

            var accountDocRef = Accounts.Document(accountId.ToString());
            var indexPayload = new AccountUserIndexDocument
            {
                AccountId = accountId.ToString(),
                CreatedAt = now
            };

            transaction.Create(accountDocRef, accountDoc);
            transaction.Create(indexDoc, indexPayload);

            // Opening entry is part of the same transaction to keep metadata consistent.
            var openingEntry = new LedgerEntryDocument
            {
                Id = Guid.NewGuid().ToString(),
                AccountId = accountId.ToString(),
                Type = LedgerEntryType.Credit.ToString(),
                Amount = 0.0d,
                BalanceAfter = 0.0d,
                Description = "Account opened",
                CreatedAt = now
            };

            var ledgerDoc = accountDocRef
                .Collection(FirestoreCollections.LedgerEntries)
                .Document(openingEntry.Id);
            transaction.Create(ledgerDoc, openingEntry);

            return new AccountCreateResult(accountId, accountNumber, Created: true);
        }, null, ct);
    }

    public async Task UpdateAsync(Account account, CancellationToken ct = default)
    {
        var docRef = Accounts.Document(account.Id.ToString());
        var doc = new AccountDocument
        {
            Id = account.Id.ToString(),
            UserId = account.UserId.ToString(),
            AccountNumber = account.AccountNumber,
            Type = account.Type.ToString(),
            Currency = account.Currency,
            Balance = (double)account.Balance,
            LedgerCount = account.LedgerCount,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt
        };

        await docRef.SetAsync(doc);
    }

    private static Account ToEntity(AccountDocument doc) =>
        new()
        {
            Id = Guid.Parse(doc.Id),
            UserId = Guid.Parse(doc.UserId),
            AccountNumber = doc.AccountNumber,
            Type = Enum.Parse<AccountType>(doc.Type),
            Currency = doc.Currency,
            Balance = ToDecimal(doc.Balance),
            LedgerCount = doc.LedgerCount,
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt
        };

    private static decimal ToDecimal(double value) =>
        Math.Round((decimal)value, 3, MidpointRounding.AwayFromZero);
}
