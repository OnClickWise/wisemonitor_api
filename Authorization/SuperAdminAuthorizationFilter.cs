using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using WiseMonitor.Api.Models.Enums;

namespace WiseMonitor.Api.Authorization
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class SuperAdminOnlyAttribute : TypeFilterAttribute
    {
        public SuperAdminOnlyAttribute() : base(typeof(SuperAdminAuthorizationFilter)) { }
    }

    public class SuperAdminAuthorizationFilter : IAuthorizationFilter
    {
        private readonly ILogger<SuperAdminAuthorizationFilter> _logger;

        public SuperAdminAuthorizationFilter(ILogger<SuperAdminAuthorizationFilter> logger)
            => _logger = logger;

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            if (user.Identity?.IsAuthenticated != true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var isSuperAdmin = user.IsInRole(UserRoles.SuperAdmin)
                || string.Equals(user.FindFirst("isSuperAdmin")?.Value, "true", StringComparison.OrdinalIgnoreCase);

            if (!isSuperAdmin)
            {
                _logger.LogWarning("[SuperAdmin] Acesso negado para usuário {UserId}",
                    user.FindFirst("sub")?.Value ?? user.FindFirst("userId")?.Value);

                context.Result = new ForbidResult();
            }
        }
    }
}
