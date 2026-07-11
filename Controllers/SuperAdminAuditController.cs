using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.DTOs.AuditLog;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [Route("api/super-admin/audit-logs")]
    public class SuperAdminAuditController : SuperAdminBaseController
    {
        private readonly IAuditService _audit;

        public SuperAdminAuditController(IAuditService audit) => _audit = audit;

        /// <summary>Lista logs de auditoria de toda a plataforma (sem filtro por tenant).</summary>
        [HttpGet]
        [HasPermission(Permissions.AuditLogsView)]
        public async Task<IActionResult> GetAll([FromQuery] AuditLogFilterDTO filter, CancellationToken ct)
        {
            // Passa null = sem filtro de org → SuperAdmin vê todos os tenants
            var result = await _audit.GetLogsAsync(filter, organizationId: null);
            return OkResponse(result);
        }

        /// <summary>Exporta logs em CSV.</summary>
        [HttpGet("export")]
        [HasPermission(Permissions.AuditLogsView)]
        public async Task<IActionResult> Export([FromQuery] AuditLogFilterDTO filter, CancellationToken ct)
        {
            filter.PageSize = 10_000;
            filter.Page = 1;

            var result = await _audit.GetLogsAsync(filter, organizationId: null);

            var sb = new StringBuilder();
            sb.AppendLine("Id,OrganizationId,UserEmail,UserRole,Action,EntityType,EntityId,IpAddress,Success,Details,CreatedAt");

            foreach (var log in result.Items)
            {
                sb.AppendLine(string.Join(",",
                    log.Id,
                    log.OrganizationId?.ToString() ?? "",
                    Escape(log.UserEmail),
                    Escape(log.UserRole),
                    Escape(log.Action),
                    Escape(log.EntityType),
                    Escape(log.EntityId ?? ""),
                    Escape(log.IpAddress ?? ""),
                    log.Success,
                    Escape(log.Details ?? ""),
                    log.CreatedAt.ToString("o")));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"audit-log-{System.DateTime.UtcNow:yyyy-MM-dd}.csv");
        }

        private static string Escape(string value) =>
            value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;
    }
}
