using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

namespace WalletService.API.Services;

public class UserLookupService : IUserLookupService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserLookupService> _logger;
    private readonly string _authServiceUrl;

    public UserLookupService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<UserLookupService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _authServiceUrl = configuration["Services:AuthServiceUrl"] ?? "http://auth-service:8080";

        var apiKey = configuration["InternalApi:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Internal-Api-Key", apiKey);
        }
    }

    public async Task<Guid?> GetUserIdByEmailAsync(string email)
    {
        try
        {
            _logger.LogDebug("Looking up user ID for email: {Email}", email);

            var response = await _httpClient.GetAsync(
                $"{_authServiceUrl}/api/auth/internal/user/by-email/{Uri.EscapeDataString(email)}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to lookup user by email. Status: {Status}", response.StatusCode);
                return null;
            }

            var userLookup = await response.Content.ReadFromJsonAsync<InternalUserLookupDto>();
            if (userLookup?.UserId == null)
            {
                _logger.LogWarning("User not found for email: {Email}", email);
                return null;
            }

            _logger.LogDebug("Found user ID {UserId} for email: {Email}", userLookup.UserId.Value, email);
            return userLookup.UserId.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up user ID for email: {Email}", email);
            return null;
        }
    }

    public async Task<string?> GetUserNameAsync(Guid userId)
    {
        try
        {
            _logger.LogDebug("Looking up user name for user ID: {UserId}", userId);

            // For now, we'll return null as we don't have a direct endpoint for this
            // In the future, we could add an internal endpoint for user lookup by ID
            _logger.LogWarning("User name lookup by ID not implemented yet");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up user name for user ID: {UserId}", userId);
            return null;
        }
    }

    private record InternalUserLookupDto(Guid? UserId, string? Name, string? Email);
}