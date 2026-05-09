using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using PaymentService.API.Entities;

namespace PaymentService.API.Persistence;

public static class FirestoreCollections
{
    public const string TopUpTransactions = "top_up_transactions";
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

public interface IPaymentTransactionRepository
{
    Task CreateAsync(TopUpTransaction transaction, CancellationToken ct = default);
    Task UpdateAsync(TopUpTransaction transaction, CancellationToken ct = default);
    Task<TopUpTransaction?> GetByIdAsync(Guid transactionId, CancellationToken ct = default);
    Task<TopUpTransaction?> GetByStripeSessionIdAsync(string sessionId, CancellationToken ct = default);
    Task<TopUpTransaction?> GetByStripePaymentIntentIdAsync(string paymentIntentId, CancellationToken ct = default);
    Task<TopUpTransaction?> GetByStripeEventIdAsync(string eventId, CancellationToken ct = default);
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
