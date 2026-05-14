using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace PaymentService.API.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireAdminAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var userRole = user.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(new { message = "Forbidden. Required role: Admin." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
