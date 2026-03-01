using AuthService.API.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace AuthService.API.Attributes;

/// <summary>
/// Attribute-based RBAC authorization filter.
/// Returns 403 Forbidden (with message body) when the authenticated user lacks the required role.
/// Returns 401 Unauthorized when the request has no valid identity.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _roles;

    public RequireRoleAttribute(params UserRole[] roles)
    {
        _roles = roles.Select(r => r.ToString()).ToArray();
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var userRole = user.FindFirstValue(ClaimTypes.Role);
        if (userRole is null || !_roles.Contains(userRole))
        {
            context.Result = new ObjectResult(
                new { message = $"Forbidden. Required role: {string.Join(" or ", _roles)}." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
