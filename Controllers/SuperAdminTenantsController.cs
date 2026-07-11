using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [Route("api/super-admin/tenants")]
    public class SuperAdminTenantsController : SuperAdminBaseController
    {
        private readonly ISuperAdminTenantService _service;

        public SuperAdminTenantsController(ISuperAdminTenantService service)
            => _service = service;

        /// <summary>Lista todos os tenants com filtros e paginação.</summary>
        [HttpGet]
        [HasPermission(Permissions.TenantsView)]
        public async Task<IActionResult> GetAll([FromQuery] TenantFilterDTO filter, CancellationToken ct)
        {
            var result = await _service.GetAllAsync(filter, ct);
            return OkResponse(result);
        }

        /// <summary>Obtém um tenant pelo ID.</summary>
        [HttpGet("{id:guid}")]
        [HasPermission(Permissions.TenantsView)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var tenant = await _service.GetByIdAsync(id, ct);
            return tenant is null ? NotFoundResponse() : OkResponse(tenant);
        }

        /// <summary>Cria um novo tenant (organização + admin).</summary>
        [HttpPost]
        [HasPermission(Permissions.TenantsCreate)]
        public async Task<IActionResult> Create([FromBody] CreateTenantDTO dto, CancellationToken ct)
        {
            try
            {
                var result = await _service.CreateAsync(dto, CurrentUserId, ct);
                return CreatedResponse(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        /// <summary>Atualiza campos do tenant.</summary>
        [HttpPatch("{id:guid}")]
        [HasPermission(Permissions.TenantsEdit)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantDTO dto, CancellationToken ct)
        {
            try
            {
                await _service.UpdateAsync(id, dto, ct);
                return OkResponse<object>(null!, "Tenant atualizado com sucesso");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }

        /// <summary>Exclui um tenant (irreversível).</summary>
        [HttpDelete("{id:guid}")]
        [HasPermission(Permissions.TenantsDelete)]
        public async Task<IActionResult> Delete(Guid id, [FromQuery] string reason = "Excluído pelo SuperAdmin", CancellationToken ct = default)
        {
            try
            {
                await _service.DeleteAsync(id, reason, CurrentUserId, ct);
                return OkResponse<object>(null!, "Tenant removido");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }

        /// <summary>Suspende o tenant.</summary>
        [HttpPost("{id:guid}/suspend")]
        [HasPermission(Permissions.TenantsSuspend)]
        public async Task<IActionResult> Suspend(Guid id, [FromBody] SuspendTenantDTO dto, CancellationToken ct)
        {
            try
            {
                await _service.SuspendAsync(id, dto, CurrentUserId, ct);
                return OkResponse<object>(null!, "Tenant suspenso com sucesso");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestResponse(ex.Message);
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }

        /// <summary>Reativa o tenant.</summary>
        [HttpPost("{id:guid}/activate")]
        [HasPermission(Permissions.TenantsSuspend)]
        public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
        {
            try
            {
                await _service.ActivateAsync(id, CurrentUserId, ct);
                return OkResponse<object>(null!, "Tenant ativado com sucesso");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }

        /// <summary>Altera o plano do tenant.</summary>
        [HttpPut("{id:guid}/plan")]
        [HasPermission(Permissions.TenantsEdit)]
        public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] UpdatePlanDTO dto, CancellationToken ct)
        {
            try
            {
                await _service.UpdatePlanAsync(id, dto, CurrentUserId, ct);
                return OkResponse<object>(null!, "Plano atualizado");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }

        /// <summary>Retorna estatísticas detalhadas do tenant.</summary>
        [HttpGet("{id:guid}/stats")]
        [HasPermission(Permissions.TenantsView)]
        public async Task<IActionResult> GetStats(Guid id, CancellationToken ct)
        {
            var stats = await _service.GetStatsAsync(id, ct);
            return stats is null ? NotFoundResponse() : OkResponse(stats);
        }

        /// <summary>Lista usuários do tenant.</summary>
        [HttpGet("{id:guid}/users")]
        [HasPermission(Permissions.TenantsView)]
        public async Task<IActionResult> GetUsers(Guid id, [FromQuery] PaginationDTO pagination, CancellationToken ct)
        {
            var users = await _service.GetUsersAsync(id, pagination, ct);
            return OkResponse(users);
        }

        /// <summary>Redefine a senha do admin do tenant e retorna senha temporária.</summary>
        [HttpPost("{id:guid}/reset-admin-password")]
        [HasPermission(Permissions.TenantsEdit)]
        public async Task<IActionResult> ResetAdminPassword(Guid id, CancellationToken ct)
        {
            try
            {
                var tempPassword = await _service.ResetAdminPasswordAsync(id, CurrentUserId, ct);
                return OkResponse(new { TemporaryPassword = tempPassword });
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        /// <summary>Gera token de impersonação (30min) para acessar o painel do tenant.</summary>
        [HttpPost("{id:guid}/impersonate")]
        [HasPermission(Permissions.TenantsEdit)]
        public async Task<IActionResult> Impersonate(Guid id, CancellationToken ct)
        {
            try
            {
                var token = await _service.GenerateImpersonationTokenAsync(id, CurrentUserId, ct);
                return OkResponse(new { Token = token, ExpiresIn = 1800 });
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        /// <summary>Obtém configurações de branding do tenant.</summary>
        [HttpGet("{id:guid}/branding")]
        [HasPermission(Permissions.TenantsView)]
        public async Task<IActionResult> GetBranding(Guid id, CancellationToken ct)
        {
            var branding = await _service.GetBrandingAsync(id, ct);
            return branding is null ? NotFoundResponse() : OkResponse(branding);
        }

        /// <summary>Atualiza configurações de branding do tenant.</summary>
        [HttpPut("{id:guid}/branding")]
        [HasPermission(Permissions.TenantsEdit)]
        public async Task<IActionResult> UpdateBranding(Guid id, [FromBody] UpdateBrandingDTO dto, CancellationToken ct)
        {
            try
            {
                await _service.UpdateBrandingAsync(id, dto, ct);
                return OkResponse<object>(null!, "Branding atualizado com sucesso");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }
    }
}
