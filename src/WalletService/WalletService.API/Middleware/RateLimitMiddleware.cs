using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace WalletService.API.Middleware;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private readonly RateLimiter _rateLimiter;

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _rateLimiter = CreateRateLimiter(configuration);
    }

    private RateLimiter CreateRateLimiter(IConfiguration configuration)
    {
        var rateLimitPerMinute = configuration.GetValue<int>("RateLimit:PerMinute", 100);
        var rateLimitPerHour = configuration.GetValue<int>("RateLimit:PerHour", 1000);

        return new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = rateLimitPerMinute,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 10,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10,
            QueueLimitReached = (context) =>
            {
                _logger.LogWarning("Rate limit queue reached for {RemoteIpAddress}", context.HttpContext.Connection.RemoteIpAddress);
                return new RateLimitLeaseInfo(true, "Rate limit queue reached");
            }
        });
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using var lease = await _rateLimiter.AttemptAcquireAsync();

        if (lease.IsAcquired)
        {
            await _next(context);
        }
        else
        {
            _logger.LogWarning("Rate limit exceeded for {RemoteIpAddress}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
        }
    }
}