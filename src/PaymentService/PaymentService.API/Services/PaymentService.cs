using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PaymentService.API.DTOs;
using PaymentService.API.Entities;
using PaymentService.API.Persistence;

namespace PaymentService.API.Services;

public interface IPaymentService
{
    Task<CreateCheckoutSessionResponseDto> CreateCheckoutSessionAsync(
        string bearerToken,
        Guid userId,
        decimal amount,
        string? returnBaseUrl,
        CancellationToken ct = default);

    Task<StripeWebhookResponseDto> HandleStripeWebhookAsync(
        string payload,
        string signatureHeader,
        CancellationToken ct = default);

    Task<CreateWithdrawResponseDto> CreateWithdrawRequestAsync(
        string bearerToken,
        Guid userId,
        decimal amount,
        string? notes,
        CancellationToken ct = default);

    Task<IReadOnlyList<WithdrawalRequestDto>> GetWithdrawRequestsAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<IReadOnlyList<WithdrawalRequestDto>> GetAllWithdrawRequestsAsync(
        string? status,
        CancellationToken ct = default);

    Task<WithdrawalRequestDto?> ApproveWithdrawalAsync(
        Guid withdrawalRequestId,
        Guid adminId,
        CancellationToken ct = default);

    Task<WithdrawalRequestDto?> RejectWithdrawalAsync(
        Guid withdrawalRequestId,
        Guid adminId,
        string? rejectionReason,
        CancellationToken ct = default);
}

public interface IWalletServiceClient
{
    Task<WalletSummaryDto?> GetDefaultWalletAsync(string bearerToken, Guid userId, CancellationToken ct = default);
    Task<InternalTopUpResponseDto> CreditTopUpAsync(InternalTopUpRequestDto request, CancellationToken ct = default);
    Task<InternalWithdrawResponseDto> DebitWithdrawalAsync(InternalWithdrawRequestDto request, CancellationToken ct = default);
}

public sealed class WalletServiceClient : IWalletServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly InternalApiOptions _internalApiOptions;

    public WalletServiceClient(HttpClient httpClient, IOptions<InternalApiOptions> internalApiOptions)
    {
        _httpClient = httpClient;
        _internalApiOptions = internalApiOptions.Value;
    }

    public async Task<WalletSummaryDto?> GetDefaultWalletAsync(string bearerToken, Guid userId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/wallet/wallets");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var wallets = await JsonSerializer.DeserializeAsync<List<WalletSummaryDto>>(
            stream,
            JsonOptions,
            ct) ?? new List<WalletSummaryDto>();

        var selected = wallets.FirstOrDefault(wallet => wallet.IsDefault)
            ?? wallets.FirstOrDefault(wallet => wallet.IsActive);
        if (selected is not null)
        {
            return selected;
        }

        using var balanceRequest = new HttpRequestMessage(HttpMethod.Get, "/api/wallet/balance");
        balanceRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var balanceResponse = await _httpClient.SendAsync(balanceRequest, ct);
        if (balanceResponse.IsSuccessStatusCode)
        {
            await using var balanceStream = await balanceResponse.Content.ReadAsStreamAsync(ct);
            var balance = await JsonSerializer.DeserializeAsync<WalletBalanceDto>(
                balanceStream,
                JsonOptions,
                ct);
            if (balance is null)
            {
                return null;
            }

            return new WalletSummaryDto(
                Id: balance.AccountId.ToString(),
                Name: "Default Wallet",
                Type: "checking",
                Balance: balance.Balance,
                Currency: balance.Currency,
                IsActive: true,
                CreatedAt: balance.UpdatedAt,
                IsDefault: true);
        }

        // No wallet exists yet for this user; provision one through internal API and retry once.
        using var provisionRequest = new HttpRequestMessage(HttpMethod.Post, "/api/internal/wallet/provision")
        {
            Content = JsonContent.Create(new
            {
                userId,
                currency = "USD"
            })
        };
        provisionRequest.Headers.TryAddWithoutValidation("X-Internal-Api-Key", _internalApiOptions.ApiKey);

        using var provisionResponse = await _httpClient.SendAsync(provisionRequest, ct);
        if (!provisionResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using var retryBalanceRequest = new HttpRequestMessage(HttpMethod.Get, "/api/wallet/balance");
        retryBalanceRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var retryBalanceResponse = await _httpClient.SendAsync(retryBalanceRequest, ct);
        if (!retryBalanceResponse.IsSuccessStatusCode)
        {
            return null;
        }

        await using var retryBalanceStream = await retryBalanceResponse.Content.ReadAsStreamAsync(ct);
        var retryBalance = await JsonSerializer.DeserializeAsync<WalletBalanceDto>(
            retryBalanceStream,
            JsonOptions,
            ct);
        if (retryBalance is null)
        {
            return null;
        }

        return new WalletSummaryDto(
            Id: retryBalance.AccountId.ToString(),
            Name: "Default Wallet",
            Type: "checking",
            Balance: retryBalance.Balance,
            Currency: retryBalance.Currency,
            IsActive: true,
            CreatedAt: retryBalance.UpdatedAt,
            IsDefault: true);
    }

    public async Task<InternalTopUpResponseDto> CreditTopUpAsync(InternalTopUpRequestDto request, CancellationToken ct = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/internal/wallet/topup")
        {
            Content = JsonContent.Create(request)
        };

        httpRequest.Headers.TryAddWithoutValidation("X-Internal-Api-Key", _internalApiOptions.ApiKey);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            return await JsonSerializer.DeserializeAsync<InternalTopUpResponseDto>(
                stream,
                JsonOptions,
                ct) ?? new InternalTopUpResponseDto(false, "Unable to parse wallet credit response.", request.WalletId, 0, request.Currency, null, false);
        }

        var fallback = await JsonSerializer.DeserializeAsync<InternalTopUpResponseDto>(
            stream,
            JsonOptions,
            ct) ?? new InternalTopUpResponseDto(false, "Wallet credit failed.", request.WalletId, 0, request.Currency, null, false);

        return fallback with
        {
            Success = false,
            ErrorMessage = fallback.ErrorMessage ?? "Wallet credit failed."
        };
    }

    public async Task<InternalWithdrawResponseDto> DebitWithdrawalAsync(InternalWithdrawRequestDto request, CancellationToken ct = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/internal/wallet/withdraw")
        {
            Content = JsonContent.Create(request)
        };

        httpRequest.Headers.TryAddWithoutValidation("X-Internal-Api-Key", _internalApiOptions.ApiKey);

        using var response = await _httpClient.SendAsync(httpRequest, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            return await JsonSerializer.DeserializeAsync<InternalWithdrawResponseDto>(
                stream,
                JsonOptions,
                ct) ?? new InternalWithdrawResponseDto(false, "Unable to parse wallet debit response.", request.WalletId, 0, request.Currency, null, false);
        }

        var fallback = await JsonSerializer.DeserializeAsync<InternalWithdrawResponseDto>(
            stream,
            JsonOptions,
            ct) ?? new InternalWithdrawResponseDto(false, "Wallet debit failed.", request.WalletId, 0, request.Currency, null, false);

        return fallback with
        {
            Success = false,
            ErrorMessage = fallback.ErrorMessage ?? "Wallet debit failed."
        };
    }
}

public sealed class StripePaymentService : IPaymentService
{
    private static readonly TimeSpan WebhookTolerance = TimeSpan.FromMinutes(5);
    private readonly IPaymentTransactionRepository _transactions;
    private readonly IWithdrawalRequestRepository _withdrawalRequests;
    private readonly IWalletServiceClient _walletServiceClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly StripeOptions _stripeOptions;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<StripePaymentService> _logger;

    public StripePaymentService(
        IPaymentTransactionRepository transactions,
        IWithdrawalRequestRepository withdrawalRequests,
        IWalletServiceClient walletServiceClient,
        IHttpClientFactory httpClientFactory,
        IOptions<StripeOptions> stripeOptions,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<StripePaymentService> logger)
    {
        _transactions = transactions;
        _withdrawalRequests = withdrawalRequests;
        _walletServiceClient = walletServiceClient;
        _httpClientFactory = httpClientFactory;
        _stripeOptions = stripeOptions.Value;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    public async Task<CreateWithdrawResponseDto> CreateWithdrawRequestAsync(
        string bearerToken,
        Guid userId,
        decimal amount,
        string? notes,
        CancellationToken ct = default)
    {
        var normalizedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (normalizedAmount < 1m)
        {
            return new CreateWithdrawResponseDto(false, null, null, null, null, "Amount must be greater than or equal to 1.");
        }

        var wallet = await _walletServiceClient.GetDefaultWalletAsync(bearerToken, userId, ct);
        if (wallet is null)
        {
            return new CreateWithdrawResponseDto(false, null, null, null, null, "No active wallet found for the authenticated user.");
        }

        var walletCurrency = string.IsNullOrWhiteSpace(wallet.Currency) ? "USD" : wallet.Currency.ToUpperInvariant();
        var operationId = Guid.NewGuid().ToString("N");
        var debitResponse = await _walletServiceClient.DebitWithdrawalAsync(
            new InternalWithdrawRequestDto(
                userId,
                Guid.Parse(wallet.Id),
                normalizedAmount,
                walletCurrency,
                operationId,
                notes),
            ct);

        if (!debitResponse.Success)
        {
            return new CreateWithdrawResponseDto(
                false,
                null,
                null,
                null,
                walletCurrency,
                debitResponse.ErrorMessage ?? "Unable to debit wallet for withdrawal request.");
        }

        var request = new WithdrawalRequest
        {
            UserId = userId,
            WalletId = Guid.Parse(wallet.Id),
            Amount = normalizedAmount,
            Currency = walletCurrency,
            Status = WithdrawalRequestStatus.Pending.ToString(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            OperationId = operationId,
            LedgerEntryId = debitResponse.LedgerEntryId,
            BalanceAfterDebit = debitResponse.NewBalance,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _withdrawalRequests.CreateAsync(request, ct);

        // TODO: trigger Stripe payout when Connect is configured

        return new CreateWithdrawResponseDto(
            true,
            request.Id,
            request.Status,
            debitResponse.NewBalance,
            walletCurrency,
            null);
    }

    public async Task<IReadOnlyList<WithdrawalRequestDto>> GetWithdrawRequestsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var requests = await _withdrawalRequests.ListByUserIdAsync(userId, ct);

        return requests
            .OrderByDescending(request => request.CreatedAt)
            .Select(MapWithdrawal)
            .ToList();
    }

    public async Task<IReadOnlyList<WithdrawalRequestDto>> GetAllWithdrawRequestsAsync(
        string? status,
        CancellationToken ct = default)
    {
        var normalizedStatus = NormalizeWithdrawalStatus(status);
        var requests = await _withdrawalRequests.ListAllAsync(normalizedStatus, ct);

        return requests
            .OrderByDescending(request => request.CreatedAt)
            .Select(MapWithdrawal)
            .ToList();
    }

    public async Task<WithdrawalRequestDto?> ApproveWithdrawalAsync(
        Guid withdrawalRequestId,
        Guid adminId,
        CancellationToken ct = default)
    {
        var request = await _withdrawalRequests.GetByIdAsync(withdrawalRequestId, ct);
        if (request is null)
        {
            return null;
        }

        if (!string.Equals(request.Status, WithdrawalRequestStatus.Pending.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only Pending withdrawal requests can be approved.");
        }

        request.Status = WithdrawalRequestStatus.Approved.ToString();
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedBy = adminId;
        request.UpdatedAt = DateTime.UtcNow;

        // TODO: trigger Stripe payout here when Connect is configured

        await _withdrawalRequests.UpdateAsync(request, ct);
        return MapWithdrawal(request);
    }

    public async Task<WithdrawalRequestDto?> RejectWithdrawalAsync(
        Guid withdrawalRequestId,
        Guid adminId,
        string? rejectionReason,
        CancellationToken ct = default)
    {
        var request = await _withdrawalRequests.GetByIdAsync(withdrawalRequestId, ct);
        if (request is null)
        {
            return null;
        }

        if (!string.Equals(request.Status, WithdrawalRequestStatus.Pending.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only Pending withdrawal requests can be rejected.");
        }

        var refundReference = $"withdraw-reject-{request.Id:N}";
        var refundResponse = await _walletServiceClient.CreditTopUpAsync(
            new InternalTopUpRequestDto(
                request.UserId,
                request.WalletId,
                request.Amount,
                request.Currency,
                refundReference,
                refundReference,
                refundReference),
            ct);

        if (!refundResponse.Success && !refundResponse.Duplicate)
        {
            throw new InvalidOperationException(refundResponse.ErrorMessage ?? "Unable to refund wallet for rejected withdrawal.");
        }

        request.Status = WithdrawalRequestStatus.Rejected.ToString();
        request.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? null : rejectionReason.Trim();
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedBy = adminId;
        request.UpdatedAt = DateTime.UtcNow;
        request.BalanceAfterDebit = refundResponse.NewBalance;

        await _withdrawalRequests.UpdateAsync(request, ct);
        return MapWithdrawal(request);
    }

    private static string? NormalizeWithdrawalStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (Enum.TryParse<WithdrawalRequestStatus>(status, true, out var parsedStatus))
        {
            return parsedStatus.ToString();
        }

        return null;
    }

    private static WithdrawalRequestDto MapWithdrawal(WithdrawalRequest request)
    {
        return new WithdrawalRequestDto(
            request.Id,
            request.UserId,
            request.WalletId,
            request.Amount,
            request.Currency,
            request.Status,
            request.Notes,
            request.CreatedAt,
            request.ProcessedAt,
            request.ProcessedBy,
            request.RejectionReason);
    }

    public async Task<CreateCheckoutSessionResponseDto> CreateCheckoutSessionAsync(
        string bearerToken,
        Guid userId,
        decimal amount,
        string? returnBaseUrl,
        CancellationToken ct = default)
    {
        var normalizedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        if (normalizedAmount < 1m || normalizedAmount > 1000m)
        {
            return new CreateCheckoutSessionResponseDto(false, null, null, null, null, null, "Amount must be between 1 and 1000.");
        }

        var wallet = await _walletServiceClient.GetDefaultWalletAsync(bearerToken, userId, ct);
        if (wallet is null)
        {
            return new CreateCheckoutSessionResponseDto(false, null, null, null, null, null, "No active wallet found for the authenticated user.");
        }

        var transaction = new TopUpTransaction
        {
            UserId = userId,
            WalletId = Guid.Parse(wallet.Id),
            Amount = normalizedAmount,
            Currency = string.IsNullOrWhiteSpace(wallet.Currency) ? "USD" : wallet.Currency.ToUpperInvariant(),
            Status = TopUpTransactionStatus.Pending.ToString(),
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow
        };

        await _transactions.CreateAsync(transaction, ct);

        try
        {
            var stripeSession = await CreateStripeCheckoutSessionAsync(transaction, returnBaseUrl, ct);
            transaction.StripeSessionId = stripeSession.SessionId;
            await _transactions.UpdateAsync(transaction, ct);

            return new CreateCheckoutSessionResponseDto(
                true,
                stripeSession.SessionId,
                stripeSession.SessionUrl,
                transaction.Id,
                transaction.WalletId,
                transaction.Currency,
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Stripe session for transaction {TransactionId}", transaction.Id);
            transaction.Status = TopUpTransactionStatus.Failed.ToString();
            transaction.FailureReason = ex.Message;
            transaction.CompletedAt = DateTime.UtcNow;
            await _transactions.UpdateAsync(transaction, ct);

            return new CreateCheckoutSessionResponseDto(false, null, null, transaction.Id, transaction.WalletId, transaction.Currency, ex.Message);
        }
    }

    public async Task<StripeWebhookResponseDto> HandleStripeWebhookAsync(
        string payload,
        string signatureHeader,
        CancellationToken ct = default)
    {
        _logger.LogInformation("=== HANDLING STRIPE WEBHOOK ===");
        
        if (!VerifyStripeSignature(payload, signatureHeader, _stripeOptions.WebhookSecret, out var signatureError))
        {
            _logger.LogWarning("Webhook signature verification failed: {Error}", signatureError);
            return new StripeWebhookResponseDto(false, signatureError ?? "Invalid Stripe signature.");
        }

        _logger.LogInformation("Webhook signature verified successfully");

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var eventId = root.GetProperty("id").GetString() ?? string.Empty;
        var eventType = root.GetProperty("type").GetString() ?? string.Empty;
        var dataObject = root.GetProperty("data").GetProperty("object");

        _logger.LogInformation("Webhook Event - ID: {EventId}, Type: {EventType}", eventId, eventType);

        if (string.IsNullOrWhiteSpace(eventId))
        {
            _logger.LogWarning("Webhook rejected: Missing event id");
            return new StripeWebhookResponseDto(false, "Missing Stripe event id.");
        }

        var existingEvent = await _transactions.GetByStripeEventIdAsync(eventId, ct);
        if (existingEvent is not null)
        {
            _logger.LogInformation("Webhook already processed (duplicate detection). Event ID: {EventId}", eventId);
            return new StripeWebhookResponseDto(true, null);
        }

        _logger.LogInformation("Processing new webhook event: {EventType}", eventType);

        return eventType switch
        {
            "checkout.session.completed" => await HandleCheckoutSessionCompletedAsync(dataObject, eventId, ct),
            "payment_intent.succeeded" => await HandlePaymentIntentSucceededAsync(dataObject, eventId, ct),
            "payment_intent.payment_failed" => await HandlePaymentIntentFailedAsync(dataObject, eventId, ct),
            _ => new StripeWebhookResponseDto(true, null)
        };
    }

    private async Task<StripeWebhookResponseDto> HandleCheckoutSessionCompletedAsync(JsonElement dataObject, string eventId, CancellationToken ct)
    {
        var paymentStatus = dataObject.TryGetProperty("payment_status", out var paymentStatusElement)
            ? paymentStatusElement.GetString()
            : null;

        if (!string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
        {
            return new StripeWebhookResponseDto(true, null);
        }

        var transaction = await ResolveTransactionAsync(dataObject, ct);
        if (transaction is null)
        {
            return new StripeWebhookResponseDto(true, null);
        }

        var paymentIntentId = dataObject.TryGetProperty("payment_intent", out var paymentIntentElement)
            ? paymentIntentElement.GetString()
            : null;

        return await CreditWalletAndMarkSuccessAsync(transaction, paymentIntentId, eventId, sessionId: dataObject.GetProperty("id").GetString(), ct);
    }

    private async Task<StripeWebhookResponseDto> HandlePaymentIntentSucceededAsync(JsonElement dataObject, string eventId, CancellationToken ct)
    {
        var transaction = await ResolveTransactionAsync(dataObject, ct);
        if (transaction is null)
        {
            return new StripeWebhookResponseDto(true, null);
        }

        var paymentIntentId = dataObject.GetProperty("id").GetString();
        return await CreditWalletAndMarkSuccessAsync(transaction, paymentIntentId, eventId, sessionId: null, ct);
    }

    private async Task<StripeWebhookResponseDto> HandlePaymentIntentFailedAsync(JsonElement dataObject, string eventId, CancellationToken ct)
    {
        var transaction = await ResolveTransactionAsync(dataObject, ct);
        if (transaction is null)
        {
            return new StripeWebhookResponseDto(true, null);
        }

        if (transaction.Status == TopUpTransactionStatus.Succeeded.ToString())
        {
            return new StripeWebhookResponseDto(true, null);
        }

        transaction.Status = TopUpTransactionStatus.Failed.ToString();
        transaction.StripeEventId = eventId;
        transaction.StripePaymentIntentId = dataObject.GetProperty("id").GetString();
        transaction.CompletedAt = DateTime.UtcNow;
        transaction.FailureReason = dataObject.TryGetProperty("last_payment_error", out var errorElement)
            ? errorElement.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : "Stripe payment failed."
            : "Stripe payment failed.";

        await _transactions.UpdateAsync(transaction, ct);
        return new StripeWebhookResponseDto(true, null);
    }

    private async Task<StripeWebhookResponseDto> CreditWalletAndMarkSuccessAsync(
        TopUpTransaction transaction,
        string? paymentIntentId,
        string eventId,
        string? sessionId,
        CancellationToken ct)
    {
        _logger.LogInformation("=== CREDITING WALLET ===");
        _logger.LogInformation("Transaction ID: {TransactionId}, Wallet ID: {WalletId}, Amount: {Amount} {Currency}",
            transaction.Id, transaction.WalletId, transaction.Amount, transaction.Currency);
        
        if (transaction.Status == TopUpTransactionStatus.Succeeded.ToString())
        {
            _logger.LogInformation("Transaction already succeeded: {TransactionId}", transaction.Id);
            return new StripeWebhookResponseDto(true, null);
        }

        var request = new InternalTopUpRequestDto(
            transaction.UserId,
            transaction.WalletId,
            transaction.Amount,
            transaction.Currency,
            paymentIntentId ?? transaction.StripePaymentIntentId ?? string.Empty,
            eventId,
            sessionId ?? transaction.StripeSessionId ?? string.Empty);

        _logger.LogInformation("Calling WalletService to credit topup. Payment Intent: {PaymentIntentId}", request.PaymentIntentId);

        var creditResponse = await _walletServiceClient.CreditTopUpAsync(request, ct);
        
        _logger.LogInformation("WalletService response - Success: {Success}, Duplicate: {Duplicate}, Error: {Error}",
            creditResponse.Success, creditResponse.Duplicate, creditResponse.ErrorMessage);

        transaction.StripeEventId = eventId;
        transaction.StripePaymentIntentId = paymentIntentId ?? transaction.StripePaymentIntentId;
        transaction.StripeSessionId = sessionId ?? transaction.StripeSessionId;
        transaction.CompletedAt = DateTime.UtcNow;

        if (!creditResponse.Success && !creditResponse.Duplicate)
        {
            transaction.Status = TopUpTransactionStatus.Failed.ToString();
            transaction.FailureReason = creditResponse.ErrorMessage ?? "Unable to credit wallet.";
            await _transactions.UpdateAsync(transaction, ct);
            _logger.LogError("Failed to credit wallet: {Error}", transaction.FailureReason);
            return new StripeWebhookResponseDto(false, transaction.FailureReason);
        }

        transaction.Status = TopUpTransactionStatus.Succeeded.ToString();
        transaction.FailureReason = null;
        await _transactions.UpdateAsync(transaction, ct);
        _logger.LogInformation("Wallet credited successfully. New Balance: {Balance} {Currency}", 
            creditResponse.NewBalance, creditResponse.Currency);
        return new StripeWebhookResponseDto(true, null);
    }

    private async Task<TopUpTransaction?> ResolveTransactionAsync(JsonElement dataObject, CancellationToken ct)
    {
        var transactionId = TryReadMetadataValue(dataObject, "transaction_id");
        if (Guid.TryParse(transactionId, out var parsedTransactionId))
        {
            var byId = await _transactions.GetByIdAsync(parsedTransactionId, ct);
            if (byId is not null)
            {
                return byId;
            }
        }

        var paymentIntentId = dataObject.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        if (!string.IsNullOrWhiteSpace(paymentIntentId))
        {
            var byPaymentIntent = await _transactions.GetByStripePaymentIntentIdAsync(paymentIntentId, ct);
            if (byPaymentIntent is not null)
            {
                return byPaymentIntent;
            }
        }

        var sessionId = dataObject.TryGetProperty("id", out var sessionElement) ? sessionElement.GetString() : null;
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            return await _transactions.GetByStripeSessionIdAsync(sessionId, ct);
        }

        return null;
    }

    private async Task<(string SessionId, string SessionUrl)> CreateStripeCheckoutSessionAsync(TopUpTransaction transaction, string? returnBaseUrl, CancellationToken ct)
    {
        var stripeClient = _httpClientFactory.CreateClient("Stripe");
        stripeClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _stripeOptions.SecretKey);

        var amountInMinorUnits = (long)Math.Round(transaction.Amount * 100m, 0, MidpointRounding.AwayFromZero);
        var productName = $"Save Safe Wallet top-up ({transaction.Currency})";

        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "payment"),
            new("payment_method_types[0]", "card"),
            new("success_url", BuildReturnUrl(_frontendOptions.SuccessUrl, returnBaseUrl)),
            new("cancel_url", BuildReturnUrl(_frontendOptions.CancelUrl, returnBaseUrl)),
            new("line_items[0][price_data][currency]", transaction.Currency.ToLowerInvariant()),
            new("line_items[0][price_data][unit_amount]", amountInMinorUnits.ToString(CultureInfo.InvariantCulture)),
            new("line_items[0][price_data][product_data][name]", productName),
            new("line_items[0][quantity]", "1"),
            new("metadata[transaction_id]", transaction.Id.ToString()),
            new("metadata[user_id]", transaction.UserId.ToString()),
            new("metadata[wallet_id]", transaction.WalletId.ToString()),
            new("payment_intent_data[metadata][transaction_id]", transaction.Id.ToString()),
            new("payment_intent_data[metadata][user_id]", transaction.UserId.ToString()),
            new("payment_intent_data[metadata][wallet_id]", transaction.WalletId.ToString())
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/checkout/sessions")
        {
            Content = new FormUrlEncodedContent(form)
        };

        using var response = await stripeClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Stripe checkout session creation failed: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return (
            root.GetProperty("id").GetString() ?? throw new InvalidOperationException("Stripe session id missing."),
            root.GetProperty("url").GetString() ?? throw new InvalidOperationException("Stripe session url missing."));
    }

    private static string BuildReturnUrl(string url, string? returnBaseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        if (!string.IsNullOrWhiteSpace(returnBaseUrl) && Uri.TryCreate(url, UriKind.Absolute, out var configuredUri) && Uri.TryCreate(returnBaseUrl, UriKind.Absolute, out var baseUri))
        {
            var pathAndQuery = configuredUri.PathAndQuery;
            var baseText = baseUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
            var combined = $"{baseText}{pathAndQuery}";

            return combined.Contains("{CHECKOUT_SESSION_ID}", StringComparison.OrdinalIgnoreCase)
                ? combined
                : combined.Contains('?')
                    ? $"{combined}&session_id={{CHECKOUT_SESSION_ID}}"
                    : $"{combined}?session_id={{CHECKOUT_SESSION_ID}}";
        }

        return url.Contains("{CHECKOUT_SESSION_ID}", StringComparison.OrdinalIgnoreCase)
            ? url
            : url.Contains('?')
                ? $"{url}&session_id={{CHECKOUT_SESSION_ID}}"
                : $"{url}?session_id={{CHECKOUT_SESSION_ID}}";
    }

    private static string? TryReadMetadataValue(JsonElement dataObject, string key)
    {
        if (!dataObject.TryGetProperty("metadata", out var metadataElement))
        {
            return null;
        }

        return metadataElement.TryGetProperty(key, out var valueElement)
            ? valueElement.GetString()
            : null;
    }

    private static bool VerifyStripeSignature(
        string payload,
        string signatureHeader,
        string secret,
        out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
        {
            errorMessage = "Stripe signature verification failed.";
            return false;
        }

        long? timestamp = null;
        var signatures = new List<string>();

        foreach (var part in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2)
            {
                continue;
            }

            if (pair[0] == "t" && long.TryParse(pair[1], out var parsedTimestamp))
            {
                timestamp = parsedTimestamp;
            }
            else if (pair[0] == "v1")
            {
                signatures.Add(pair[1]);
            }
        }

        if (timestamp is null || signatures.Count == 0)
        {
            errorMessage = "Stripe signature verification failed.";
            return false;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp.Value) > (long)WebhookTolerance.TotalSeconds)
        {
            errorMessage = "Stripe webhook timestamp outside the accepted tolerance window.";
            return false;
        }

        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var computedSignature = Convert.ToHexString(computedHash).ToLowerInvariant();

        foreach (var candidate in signatures)
        {
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(candidate.ToLowerInvariant()),
                    Encoding.ASCII.GetBytes(computedSignature)))
            {
                return true;
            }
        }

        errorMessage = "Stripe webhook signature is invalid.";
        return false;
    }
}
