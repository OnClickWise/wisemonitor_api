using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.DTOs.AuditLog;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/audit-logs")]
    [Authorize]
    public class AuditLogController : ControllerBase
    {
        private readonly IAuditService _audit;

        public AuditLogController(IAuditService audit) => _audit = audit;

        [HttpGet]
        [HasPermission(Permissions.AuditLogsView)]
        public async Task<IActionResult> GetLogs([FromQuery] AuditLogFilterDTO filter)
        {
            var roleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;

            // SuperAdmin vê logs de todos os tenants; os demais só do próprio tenant
            Guid? orgId = UserRoles.Normalize(roleClaim) == UserRoles.SuperAdmin
                ? null
                : User.GetOrganizationId();

            var result = await _audit.GetLogsAsync(filter, orgId);
            return Ok(result);
        }
    }
}
