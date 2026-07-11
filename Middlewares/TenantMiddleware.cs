using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WiseMonitor.Api.Models.Enums;

namespace WiseMonitor.Api.Middlewares
{
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var user = context.User;    

            if (user.Identity?.IsAuthenticated == true)
            {
                // SuperAdmin de plataforma não pertence a nenhuma organização — skip
                var isSa = string.Equals(user.FindFirst("isSuperAdmin")?.Value, "true", StringComparison.OrdinalIgnoreCase)
                        || user.IsInRole(UserRoles.SuperAdmin);
                if (isSa)
                {
                    await _next(context);
                    return;
                }

                var orgId = user.FindFirst("orgId")?.Value;

                if (string.IsNullOrEmpty(orgId))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Organização não encontrada.");
                    return;
                }

                context.Items["OrganizationId"] = orgId;
            }

            await _next(context);
        }
    }
}