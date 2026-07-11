using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.DTOs.Delegation;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/delegations")]
    [Authorize]
    public class DelegationController : ControllerBase
    {
        private readonly IDelegationService _service;
        private readonly IAuditService _audit;

        public DelegationController(IDelegationService service, IAuditService audit)
        {
            _service = service;
            _audit = audit;
        }

        [HttpGet]
        [HasPermission(Permissions.DelegationsView)]
        public async Task<IActionResult> GetAll()
        {
            var orgId = User.GetOrganizationId();
            var result = await _service.GetAllAsync(orgId);
            return Ok(result);
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMine()
        {
            var orgId = User.GetOrganizationId();
            var userId = User.GetUserId();
            var result = await _service.GetByUserAsync(userId, orgId);
            return Ok(result);
        }

        [HttpPost]
        [HasPermission(Permissions.DelegationsCreate)]
        public async Task<IActionResult> Create([FromBody] CreateDelegationDTO dto)
        {
            var orgId = User.GetOrganizationId();
            var userId = User.GetUserId();

            var result = await _service.CreateAsync(dto, orgId, userId);

            await _audit.LogAsync(
                action: "delegation",
                entityType: "Delegation",
                entityId: result.Id.ToString(),
                details: $"Delegação criada para {result.DelegateName} de {result.StartDate:d} até {result.EndDate:d}",
                organizationId: orgId,
                userId: userId,
                userEmail: User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                userRole: User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        [HasPermission(Permissions.DelegationsCreate)]
        public async Task<IActionResult> Revoke(Guid id)
        {
            var orgId = User.GetOrganizationId();
            var userId = User.GetUserId();

            await _service.RevokeAsync(id, orgId, userId);

            await _audit.LogAsync(
                action: "delegation_revoke",
                entityType: "Delegation",
                entityId: id.ToString(),
                organizationId: orgId,
                userId: userId,
                userEmail: User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                userRole: User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { message = "Delegação revogada." });
        }
    }
}
