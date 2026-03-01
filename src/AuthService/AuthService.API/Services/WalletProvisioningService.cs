using System.Text;
using System.Text.Json;

namespace AuthService.API.Services;

public interface IWalletProvisioningService
{
    Task<bool> CreateWalletForUserAsync(Guid userId, string currency = "USD",
        CancellationToken ct = default);
}

public class WalletProvisioningService : IWalletProvisioningService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WalletProvisioningService> _logger;
    private readonly IConfiguration _config;

    public WalletProvisioningService(
        HttpClient httpClient,
        ILogger<WalletProvisioningService> logger,
        IConfiguration config)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config;
    }

    public async Task<bool> CreateWalletForUserAsync(Guid userId, string currency = "USD",
        CancellationToken ct = default)
    {
        var payload = new { UserId = userId, Currency = currency };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Shared secret authenticates this internal service-to-service call
        _httpClient.DefaultRequestHeaders.Remove("X-Internal-Api-Key");
        _httpClient.DefaultRequestHeaders.Add(
            "X-Internal-Api-Key", _config["InternalApi:ApiKey"]);

        try
        {
            var response = await _httpClient.PostAsync(
                "/api/internal/wallet/provision", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Wallet provisioning failed for user {UserId}: HTTP {StatusCode}",
                    userId, (int)response.StatusCode);
                return false;
            }

            _logger.LogInformation("Wallet provisioned for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            // Registration must not fail because wallet service is unavailable.
            // The wallet can be created on first access or via a retry mechanism.
            _logger.LogError(ex,
                "Wallet provisioning call failed for user {UserId}. " +
                "Registration will still succeed.", userId);
            return false;
        }
    }
}
