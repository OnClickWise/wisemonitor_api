using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace WiseMonitor.Api.Authorization
{
    public class PermissionAuthorizationHandler
        : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value
                         ?? context.User.FindFirst("role")?.Value;

            if (string.IsNullOrWhiteSpace(roleClaim))
            {
                context.Fail();
                return Task.CompletedTask;
            }

            if (RolePermissionMatrix.HasPermission(roleClaim, requirement.Permission))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
