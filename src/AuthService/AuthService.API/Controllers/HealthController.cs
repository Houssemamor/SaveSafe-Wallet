using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AuthService.API.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheck;

    public HealthController(HealthCheckService healthCheck) =>
        _healthCheck = healthCheck;

    [HttpGet]
    public async Task <IActionResult> GetHealth()
    {
        var report = await _healthCheck.CheckHealthAsync();
        var result = new
        {
            status = report.Status.ToString(),
            service = "auth-service",
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        };

        return report.Status == HealthStatus.Healthy
            ? Ok(result)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }
}
