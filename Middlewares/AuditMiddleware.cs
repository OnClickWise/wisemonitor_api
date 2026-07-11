using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Middlewares
{
    /// <summary>
    /// Registra tentativas de acesso negadas (403) e logins (via AuthController).
    /// </summary>
    public class AuditMiddleware
    {
        private readonly RequestDelegate _next;

        public AuditMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, IAuditService auditService)
        {
            await _next(context);

            // Registra acessos negados automaticamente
            if (context.Response.StatusCode == 403)
            {
                var userId  = GetUserId(context);
                var email   = context.User?.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
                var role    = context.User?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
                var orgId   = GetOrgId(context);

                await auditService.LogAsync(
                    action: "access_denied",
                    entityType: "Endpoint",
                    entityId: context.Request.Path,
                    details: $"{context.Request.Method} {context.Request.Path}",
                    success: false,
                    organizationId: orgId,
                    userId: userId,
                    userEmail: email,
                    userRole: UserRoles.Normalize(role),
                    ipAddress: context.Connection.RemoteIpAddress?.ToString(),
                    userAgent: context.Request.Headers["User-Agent"].ToString());
            }
        }

        private static Guid? GetUserId(HttpContext ctx)
        {
            var claim = ctx.User?.FindFirst(ClaimTypes.NameIdentifier)
                     ?? ctx.User?.FindFirst("sub");
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }

        private static Guid? GetOrgId(HttpContext ctx)
        {
            var claim = ctx.User?.FindFirst("orgId");
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }
    }
}
