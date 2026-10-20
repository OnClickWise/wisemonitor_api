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
        private readonly IAccessScopeService _scopeService;

        public DelegationController(IDelegationService service, IAuditService audit, IAccessScopeService scopeService)
        {
            _service = service;
            _audit = audit;
            _scopeService = scopeService;
        }

        [HttpGet]
        [HasPermission(Permissions.DelegationsView)]
        public async Task<IActionResult> GetAll()
        {
            var orgId = User.GetOrganizationId();
            var result = await _service.GetAllAsync(orgId);

            // Fora dos administradores: só delegações entre pessoas do próprio escopo.
            var scope = await _scopeService.GetScopeAsync(User);
            if (scope.IsRestricted)
                result = result.Where(d => scope.CanAccessUser(d.DelegatorId) && scope.CanAccessUser(d.DelegateId)).ToList();

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

            // Delegar só para quem está no próprio escopo, e só equipes que gere.
            var scope = await _scopeService.GetScopeAsync(User);
            if (!await _scopeService.CanAccessUserAsync(User, dto.DelegateId) ||
                dto.Scope.TeamIds.Any(t => !scope.CanAccessTeam(t)))
                return Forbid();

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
