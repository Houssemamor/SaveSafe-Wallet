using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using PaymentService.API.Entities;

namespace PaymentService.API.Persistence;

public static class FirestoreCollections
{
    public const string TopUpTransactions = "top_up_transactions";
    public const string WithdrawalRequests = "withdrawal_requests";
}

public sealed class FirestoreOptions
{
    public const string SectionName = "Firestore";
    public string ProjectId { get; set; } = string.Empty;
    public string CredentialsPath { get; set; } = string.Empty;
}

public sealed class InternalApiOptions
{
    public const string SectionName = "InternalApi";
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
}

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
}

public sealed class ServicesOptions
{
    public const string SectionName = "Services";
    public string WalletServiceUrl { get; set; } = string.Empty;
}

public interface IFirestoreDbProvider
{
    FirestoreDb GetDb();
}

public sealed class FirestoreDbProvider : IFirestoreDbProvider
{
    private readonly Lazy<FirestoreDb> _db;

    public FirestoreDbProvider(IConfiguration configuration)
    {
        _db = new Lazy<FirestoreDb>(() => CreateDb(configuration));
    }

    public FirestoreDb GetDb() => _db.Value;

    private static FirestoreDb CreateDb(IConfiguration configuration)
    {
        var projectId = configuration[$"{FirestoreOptions.SectionName}:ProjectId"]
            ?? throw new InvalidOperationException("Firestore project ID is required.");
        var credentialsPath = configuration[$"{FirestoreOptions.SectionName}:CredentialsPath"];

        var builder = new FirestoreDbBuilder
        {
            ProjectId = projectId
        };

        if (!string.IsNullOrWhiteSpace(credentialsPath) && File.Exists(credentialsPath))
        {
            builder.Credential = GoogleCredential.FromFile(credentialsPath);
        }

        return builder.Build();
    }
}

[FirestoreData]
public sealed class TopUpTransactionDocument
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [FirestoreProperty("walletId")]
    public string WalletId { get; set; } = string.Empty;

    [FirestoreProperty("stripeSessionId")]
    public string? StripeSessionId { get; set; }

    [FirestoreProperty("stripePaymentIntentId")]
    public string? StripePaymentIntentId { get; set; }

    [FirestoreProperty("stripeEventId")]
    public string? StripeEventId { get; set; }

    [FirestoreProperty("amount")]
    public double Amount { get; set; }

    [FirestoreProperty("currency")]
    public string Currency { get; set; } = string.Empty;

    [FirestoreProperty("status")]
    public string Status { get; set; } = string.Empty;

    [FirestoreProperty("idempotencyKey")]
    public string? IdempotencyKey { get; set; }

    [FirestoreProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    [FirestoreProperty("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [FirestoreProperty("failureReason")]
    public string? FailureReason { get; set; }
}

[FirestoreData]
public sealed class WithdrawalRequestDocument
{
    [FirestoreDocumentId]
    public string Id { get; set; } = string.Empty;

    [FirestoreProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [FirestoreProperty("walletId")]
    public string WalletId { get; set; } = string.Empty;

    [FirestoreProperty("amount")]
    public double Amount { get; set; }

    [FirestoreProperty("currency")]
    public string Currency { get; set; } = string.Empty;

    [FirestoreProperty("status")]
    public string Status { get; set; } = string.Empty;

    [FirestoreProperty("notes")]
    public string? Notes { get; set; }

    [FirestoreProperty("operationId")]
    public string? OperationId { get; set; }

    [FirestoreProperty("ledgerEntryId")]
    public string? LedgerEntryId { get; set; }

    [FirestoreProperty("balanceAfterDebit")]
    public double? BalanceAfterDebit { get; set; }

    [FirestoreProperty("failureReason")]
    public string? FailureReason { get; set; }

    [FirestoreProperty("rejectionReason")]
    public string? RejectionReason { get; set; }

    [FirestoreProperty("processedAt")]
    public DateTime? ProcessedAt { get; set; }

    [FirestoreProperty("processedBy")]
    public string? ProcessedBy { get; set; }

    [FirestoreProperty("createdAt")]
    public DateTime CreatedAt { get; set; }

    [FirestoreProperty("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}

public interface IPaymentTransactionRepository
{
    Task CreateAsync(TopUpTransaction transaction, CancellationToken ct = default);
    Task UpdateAsync(TopUpTransaction transaction, CancellationToken ct = default);
    Task<TopUpTransaction?> GetByIdAsync(Guid transactionId, CancellationToken ct = default);
    Task<TopUpTransaction?> GetByStripeSessionIdAsync(string sessionId, CancellationToken ct = default);
    Task<TopUpTransaction?> GetByStripePaymentIntentIdAsync(string paymentIntentId, CancellationToken ct = default);
    Task<TopUpTransaction?> GetByStripeEventIdAsync(string eventId, CancellationToken ct = default);
}

public interface IWithdrawalRequestRepository
{
    Task CreateAsync(WithdrawalRequest request, CancellationToken ct = default);
    Task UpdateAsync(WithdrawalRequest request, CancellationToken ct = default);
    Task<WithdrawalRequest?> GetByIdAsync(Guid requestId, CancellationToken ct = default);
    Task<IReadOnlyList<WithdrawalRequest>> ListByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<WithdrawalRequest>> ListAllAsync(string? status, CancellationToken ct = default);
}

public sealed class PaymentTransactionRepository : IPaymentTransactionRepository
{
    private readonly IFirestoreDbProvider _dbProvider;

    public PaymentTransactionRepository(IFirestoreDbProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    private FirestoreDb Db => _dbProvider.GetDb();

    private CollectionReference Transactions => Db.Collection(FirestoreCollections.TopUpTransactions);

    public async Task CreateAsync(TopUpTransaction transaction, CancellationToken ct = default)
    {
        var doc = ToDocument(transaction);
        await Transactions.Document(doc.Id).SetAsync(doc, cancellationToken: ct);
    }

    public async Task UpdateAsync(TopUpTransaction transaction, CancellationToken ct = default)
    {
        var doc = ToDocument(transaction);
        await Transactions.Document(doc.Id).SetAsync(doc, cancellationToken: ct);
    }

    public async Task<TopUpTransaction?> GetByIdAsync(Guid transactionId, CancellationToken ct = default)
    {
        var snapshot = await Transactions.Document(transactionId.ToString()).GetSnapshotAsync(ct);
        return snapshot.Exists ? ToEntity(snapshot.ConvertTo<TopUpTransactionDocument>()) : null;
    }

    public async Task<TopUpTransaction?> GetByStripeSessionIdAsync(string sessionId, CancellationToken ct = default)
    {
        return await QuerySingleAsync("stripeSessionId", sessionId, ct);
    }

    public async Task<TopUpTransaction?> GetByStripePaymentIntentIdAsync(string paymentIntentId, CancellationToken ct = default)
    {
        return await QuerySingleAsync("stripePaymentIntentId", paymentIntentId, ct);
    }

    public async Task<TopUpTransaction?> GetByStripeEventIdAsync(string eventId, CancellationToken ct = default)
    {
        return await QuerySingleAsync("stripeEventId", eventId, ct);
    }

    private async Task<TopUpTransaction?> QuerySingleAsync(string fieldName, string value, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var snapshot = await Transactions
            .WhereEqualTo(fieldName, value)
            .Limit(1)
            .GetSnapshotAsync(ct);

        var document = snapshot.Documents.FirstOrDefault();
        return document is null ? null : ToEntity(document.ConvertTo<TopUpTransactionDocument>());
    }

    private static TopUpTransactionDocument ToDocument(TopUpTransaction transaction) => new()
    {
        Id = transaction.Id.ToString(),
        UserId = transaction.UserId.ToString(),
        WalletId = transaction.WalletId.ToString(),
        StripeSessionId = transaction.StripeSessionId,
        StripePaymentIntentId = transaction.StripePaymentIntentId,
        StripeEventId = transaction.StripeEventId,
        Amount = (double)transaction.Amount,
        Currency = transaction.Currency,
        Status = transaction.Status,
        IdempotencyKey = transaction.IdempotencyKey,
        CreatedAt = transaction.CreatedAt,
        CompletedAt = transaction.CompletedAt,
        FailureReason = transaction.FailureReason
    };

    private static TopUpTransaction ToEntity(TopUpTransactionDocument document) => new()
    {
        Id = Guid.Parse(document.Id),
        UserId = Guid.Parse(document.UserId),
        WalletId = Guid.Parse(document.WalletId),
        StripeSessionId = document.StripeSessionId,
        StripePaymentIntentId = document.StripePaymentIntentId,
        StripeEventId = document.StripeEventId,
        Amount = (decimal)document.Amount,
        Currency = document.Currency,
        Status = document.Status,
        IdempotencyKey = document.IdempotencyKey,
        CreatedAt = document.CreatedAt,
        CompletedAt = document.CompletedAt,
        FailureReason = document.FailureReason
    };
}

public sealed class WithdrawalRequestRepository : IWithdrawalRequestRepository
{
    private readonly IFirestoreDbProvider _dbProvider;

    public WithdrawalRequestRepository(IFirestoreDbProvider dbProvider)
    {
        _dbProvider = dbProvider;
    }

    private FirestoreDb Db => _dbProvider.GetDb();

    private CollectionReference Requests => Db.Collection(FirestoreCollections.WithdrawalRequests);

    public async Task CreateAsync(WithdrawalRequest request, CancellationToken ct = default)
    {
        var doc = ToDocument(request);
        await Requests.Document(doc.Id).SetAsync(doc, cancellationToken: ct);
    }

    public async Task UpdateAsync(WithdrawalRequest request, CancellationToken ct = default)
    {
        var doc = ToDocument(request);
        await Requests.Document(doc.Id).SetAsync(doc, cancellationToken: ct);
    }

    public async Task<WithdrawalRequest?> GetByIdAsync(Guid requestId, CancellationToken ct = default)
    {
        var snapshot = await Requests.Document(requestId.ToString()).GetSnapshotAsync(ct);
        return snapshot.Exists ? ToEntity(snapshot.ConvertTo<WithdrawalRequestDocument>()) : null;
    }

    public async Task<IReadOnlyList<WithdrawalRequest>> ListByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var snapshot = await Requests
            .WhereEqualTo("userId", userId.ToString())
            .OrderByDescending("createdAt")
            .GetSnapshotAsync(ct);

        return snapshot.Documents
            .Select(document => ToEntity(document.ConvertTo<WithdrawalRequestDocument>()))
            .ToList();
    }

    public async Task<IReadOnlyList<WithdrawalRequest>> ListAllAsync(string? status, CancellationToken ct = default)
    {
        Query query = Requests.OrderByDescending("createdAt");

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = Requests
                .WhereEqualTo("status", status)
                .OrderByDescending("createdAt");
        }

        var snapshot = await query.GetSnapshotAsync(ct);

        return snapshot.Documents
            .Select(document => ToEntity(document.ConvertTo<WithdrawalRequestDocument>()))
            .ToList();
    }

    private static WithdrawalRequestDocument ToDocument(WithdrawalRequest request) => new()
    {
        Id = request.Id.ToString(),
        UserId = request.UserId.ToString(),
        WalletId = request.WalletId.ToString(),
        Amount = (double)request.Amount,
        Currency = request.Currency,
        Status = request.Status,
        Notes = request.Notes,
        OperationId = request.OperationId,
        LedgerEntryId = request.LedgerEntryId,
        BalanceAfterDebit = request.BalanceAfterDebit.HasValue ? (double)request.BalanceAfterDebit.Value : null,
        FailureReason = request.FailureReason,
        RejectionReason = request.RejectionReason,
        ProcessedAt = request.ProcessedAt,
        ProcessedBy = request.ProcessedBy?.ToString(),
        CreatedAt = request.CreatedAt,
        UpdatedAt = request.UpdatedAt
    };

    private static WithdrawalRequest ToEntity(WithdrawalRequestDocument document) => new()
    {
        Id = Guid.Parse(document.Id),
        UserId = Guid.Parse(document.UserId),
        WalletId = Guid.Parse(document.WalletId),
        Amount = (decimal)document.Amount,
        Currency = document.Currency,
        Status = document.Status,
        Notes = document.Notes,
        OperationId = document.OperationId,
        LedgerEntryId = document.LedgerEntryId,
        BalanceAfterDebit = document.BalanceAfterDebit.HasValue ? (decimal)document.BalanceAfterDebit.Value : null,
        FailureReason = document.FailureReason,
        RejectionReason = document.RejectionReason,
        ProcessedAt = document.ProcessedAt,
        ProcessedBy = Guid.TryParse(document.ProcessedBy, out var processedBy) ? processedBy : null,
        CreatedAt = document.CreatedAt,
        UpdatedAt = document.UpdatedAt
    };
}
