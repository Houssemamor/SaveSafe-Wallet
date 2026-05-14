using AuthService.API.DTOs;
using AuthService.API.Entities;
using AuthService.API.Persistence;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AuthService.API.Services;

public interface IAdminService
{
    Task<AdminSecuritySummaryDto> GetSecuritySummaryAsync();
    Task<AdminSecuritySummaryDto> RefreshSecuritySummaryAsync();
    Task<IReadOnlyList<AdminLoginEventDto>> GetLoginEventsAsync(int limit);
    Task<IReadOnlyList<AdminFailedLoginByIpDto>> GetFailedLoginsByIpAsync(int top);
    Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(int limit);
    Task<AdminLokiQueryResponseDto> QueryLokiAsync(AdminLokiQueryRequestDto request, CancellationToken ct = default);
    Task SuspendUserAsync(Guid userId);
    Task ActivateUserAsync(Guid userId);
    Task DeleteUserAsync(Guid userId);
}

public class AdminService : IAdminService
{
    private readonly IUserRepository _users;
    private readonly ILoginEventRepository _loginEvents;
    private readonly IFailedLoginByIpRepository _failedLoginsByIp;
    private readonly IAdminStatsRepository _adminStats;
    private readonly IAdminStatsRefresher _adminStatsRefresher;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IUserRepository users,
        ILoginEventRepository loginEvents,
        IFailedLoginByIpRepository failedLoginsByIp,
        IAdminStatsRepository adminStats,
        IAdminStatsRefresher adminStatsRefresher,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AdminService> logger)
    {
        _users = users;
        _loginEvents = loginEvents;
        _failedLoginsByIp = failedLoginsByIp;
        _adminStats = adminStats;
        _adminStatsRefresher = adminStatsRefresher;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AdminSecuritySummaryDto> GetSecuritySummaryAsync()
    {
        var snapshot = await _adminStats.GetCurrentAsync();
        if (snapshot is null)
        {
            snapshot = await _adminStatsRefresher.RefreshAsync();
        }

        return MapSnapshot(snapshot);
    }

    public async Task<AdminSecuritySummaryDto> RefreshSecuritySummaryAsync()
    {
        var snapshot = await _adminStatsRefresher.RefreshAsync();
        return MapSnapshot(snapshot);
    }

    public async Task<IReadOnlyList<AdminLoginEventDto>> GetLoginEventsAsync(int limit)
    {
        var safeLimit = Math.Clamp(limit, 1, 200);

        var events = await _loginEvents.GetRecentAsync(safeLimit);
        return events
            .Select(item => new AdminLoginEventDto(
                item.EventId,
                item.UserId,
                item.UserEmail,
                item.UserName,
                item.IpAddress,
                item.Country,
                item.Success,
                item.FailureReason,
                item.IsFlagged,
                item.Timestamp))
            .ToList();
    }

    public async Task<AdminLokiQueryResponseDto> QueryLokiAsync(AdminLokiQueryRequestDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            throw new ArgumentException("Loki query is required.");

        var safeHours = Math.Clamp(request.Hours, 1, 24);
        var safeLimit = Math.Clamp(request.Limit, 1, 50);
        var to = DateTime.UtcNow;
        var from = to.AddHours(-safeHours);

        var baseUri = _configuration["Logging:Loki:Uri"] ?? "http://loki:3100";
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(8);

        using var response = await GetLokiResponseAsync(client, baseUri, request.Query, from, to, safeLimit, ct);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (!document.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("result", out var results))
        {
            return new AdminLokiQueryResponseDto(request.Query, from, to, []);
        }

        var series = new List<AdminLokiSeriesDto>();
        foreach (var result in results.EnumerateArray())
        {
            var labels = ReadLabels(result);
            var points = ReadPoints(result);
            var name = BuildSeriesName(labels, series.Count + 1);
            var total = points.Sum(point => point.Value);

            series.Add(new AdminLokiSeriesDto(name, labels, points, total));
        }

        return new AdminLokiQueryResponseDto(request.Query, from, to, series);
    }

    private static async Task<HttpResponseMessage> GetLokiResponseAsync(
        HttpClient client,
        string baseUri,
        string query,
        DateTime from,
        DateTime to,
        int limit,
        CancellationToken ct)
    {
        try
        {
            var response = await client.GetAsync(BuildLokiQueryUrl(baseUri, query, from, to, limit), ct);
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch (HttpRequestException) when (ShouldTryLocalhostFallback(baseUri))
        {
            var fallbackResponse = await client.GetAsync(BuildLokiQueryUrl("http://localhost:3100", query, from, to, limit), ct);
            fallbackResponse.EnsureSuccessStatusCode();
            return fallbackResponse;
        }
    }

    private static string BuildLokiQueryUrl(string baseUri, string query, DateTime from, DateTime to, int limit) =>
        $"{baseUri.TrimEnd('/')}/loki/api/v1/query_range" +
        $"?query={Uri.EscapeDataString(query)}" +
        $"&start={ToUnixNano(from)}" +
        $"&end={ToUnixNano(to)}" +
        "&step=60" +
        $"&limit={limit}" +
        "&direction=forward";

    private static bool ShouldTryLocalhostFallback(string baseUri)
    {
        if (!Uri.TryCreate(baseUri, UriKind.Absolute, out var uri))
            return false;

        return string.Equals(uri.Host, "loki", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<AdminFailedLoginByIpDto>> GetFailedLoginsByIpAsync(int top)
    {
        var safeTop = Math.Clamp(top, 1, 100);

        var items = await _failedLoginsByIp.GetTopAsync(safeTop);
        return items
            .Select(item => new AdminFailedLoginByIpDto(
                item.IpAddress,
                item.FailedAttempts,
                item.LastAttemptAt))
            .ToList();
    }

    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(int limit)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);

        _logger.LogInformation("Fetching recent users with limit: {Limit}", safeLimit);

        var users = await _users.GetRecentUsersAsync(safeLimit);

        _logger.LogInformation("Retrieved {Count} users from database", users.Count);

        return users
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email,
                u.Name,
                u.Role.ToString(),
                u.AccountStatus.ToString(),
                u.MfaEnabled,
                u.CreatedAt,
                u.LastLoginAt))
            .ToList();
    }

    public async Task SuspendUserAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new KeyNotFoundException($"User {userId} not found.");

        if (user.AccountStatus == UserAccountStatus.Suspended)
            return; // Already suspended

        user.AccountStatus = UserAccountStatus.Suspended;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
    }

    public async Task ActivateUserAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new KeyNotFoundException($"User {userId} not found.");

        if (user.AccountStatus == UserAccountStatus.Active)
            return; // Already active

        user.AccountStatus = UserAccountStatus.Active;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is null)
            throw new KeyNotFoundException($"User {userId} not found.");

        if (user.AccountStatus == UserAccountStatus.Deleted)
            return; // Already deleted

        user.AccountStatus = UserAccountStatus.Deleted;
        user.UpdatedAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
    }

    private static AdminSecuritySummaryDto MapSnapshot(AdminStatsSnapshot snapshot) =>
        new(
            TotalUsers: snapshot.TotalUsers,
            ActiveUsers: snapshot.ActiveUsers,
            SuspendedUsers: snapshot.SuspendedUsers,
            DeletedUsers: snapshot.DeletedUsers,
            TotalLoginEventsLast24Hours: snapshot.TotalLoginEventsLast24Hours,
            FailedLoginEventsLast24Hours: snapshot.FailedLoginEventsLast24Hours,
            FlaggedEventsLast24Hours: snapshot.FlaggedEventsLast24Hours,
            DistinctSourceIpsLast24Hours: snapshot.DistinctSourceIpsLast24Hours,
            AiRiskScore: snapshot.AiRiskScore,
            AiRiskLevel: snapshot.AiRiskLevel,
            ComputedAt: snapshot.ComputedAt);

    private static long ToUnixNano(DateTime value) =>
        new DateTimeOffset(value).ToUnixTimeMilliseconds() * 1_000_000;

    private static Dictionary<string, string> ReadLabels(JsonElement result)
    {
        if (!result.TryGetProperty("stream", out var stream) &&
            !result.TryGetProperty("metric", out stream))
        {
            return [];
        }

        var labels = new Dictionary<string, string>();
        if (stream.ValueKind != JsonValueKind.Object)
            return labels;

        foreach (var label in stream.EnumerateObject())
        {
            labels[label.Name] = label.Value.GetString() ?? string.Empty;
        }

        return labels;
    }

    private static List<AdminLokiPointDto> ReadPoints(JsonElement result)
    {
        var points = new List<AdminLokiPointDto>();
        if (result.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Array)
        {
            foreach (var value in values.EnumerateArray())
            {
                var point = ReadPoint(value);
                if (point is not null)
                    points.Add(point);
            }
        }

        if (result.TryGetProperty("value", out var singleValue) && singleValue.ValueKind == JsonValueKind.Array)
        {
            var point = ReadPoint(singleValue);
            if (point is not null)
                points.Add(point);
        }


        return points;
    }

    private static AdminLokiPointDto? ReadPoint(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() < 2)
            return null;

        var timestampText = ReadJsonScalar(value[0]);
        var valueText = ReadJsonScalar(value[1]);

        if (!long.TryParse(timestampText, out var timestampNano) ||
            !double.TryParse(valueText, out var numericValue))
        {
            return null;
        }

        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampNano / 1_000_000).UtcDateTime;
        return new AdminLokiPointDto(timestamp, numericValue);
    }

    private static string? ReadJsonScalar(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            _ => null
        };

    private static string BuildSeriesName(IReadOnlyDictionary<string, string> labels, int fallbackIndex)
    {
        if (labels.TryGetValue("service", out var service) && !string.IsNullOrWhiteSpace(service))
            return service;

        if (labels.TryGetValue("job", out var job) && !string.IsNullOrWhiteSpace(job))
            return job;

        if (labels.TryGetValue("container", out var container) && !string.IsNullOrWhiteSpace(container))
            return container;

        return $"Series {fallbackIndex}";
    }
}
