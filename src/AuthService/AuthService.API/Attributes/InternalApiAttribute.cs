using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthService.API.Attributes;

/// <summary>
/// Attribute for internal API authentication using API key.
/// Returns 401 Unauthorized when the request lacks a valid internal API key.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class InternalApiAttribute : Attribute, IAuthorizationFilter
{
    private const string ApiKeyHeaderName = "X-Internal-Api-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expectedApiKey = configuration["InternalApi:ApiKey"];

        if (string.IsNullOrEmpty(expectedApiKey))
        {
            context.Result = new ObjectResult(
                new { message = "Internal API key not configured." })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedApiKey))
        {
            context.Result = new ObjectResult(
                new { message = "Internal API key header missing." })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        if (providedApiKey != expectedApiKey)
        {
            context.Result = new ObjectResult(
                new { message = "Invalid internal API key." })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }
    }
}