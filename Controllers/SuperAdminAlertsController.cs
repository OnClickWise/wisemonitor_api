using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [Route("api/super-admin/alerts")]
    public class SuperAdminAlertsController : SuperAdminBaseController
    {
        private readonly ISuperAdminAlertService _service;

        public SuperAdminAlertsController(ISuperAdminAlertService service)
            => _service = service;

        /// <summary>Lista as regras de alerta configuradas.</summary>
        [HttpGet("rules")]
        public async Task<IActionResult> GetRules(CancellationToken ct)
            => OkResponse(await _service.GetRulesAsync(ct));

        /// <summary>Cria uma nova regra de alerta.</summary>
        [HttpPost("rules")]
        public async Task<IActionResult> CreateRule([FromBody] CreateAlertRuleDTO dto, CancellationToken ct)
        {
            var result = await _service.CreateRuleAsync(dto, ct);
            return CreatedResponse(result);
        }

        /// <summary>Atualiza uma regra de alerta.</summary>
        [HttpPatch("rules/{id:guid}")]
        public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateAlertRuleDTO dto, CancellationToken ct)
        {
            try
            {
                await _service.UpdateRuleAsync(id, dto, ct);
                return OkResponse<object>(null!, "Regra atualizada");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }

        /// <summary>Remove uma regra de alerta.</summary>
        [HttpDelete("rules/{id:guid}")]
        public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct)
        {
            try
            {
                await _service.DeleteRuleAsync(id, ct);
                return OkResponse<object>(null!, "Regra removida");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }

        /// <summary>Histórico de alertas disparados.</summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] PaginationDTO pagination, CancellationToken ct)
            => OkResponse(await _service.GetHistoryAsync(pagination, ct));

        /// <summary>Marca um alerta como resolvido.</summary>
        [HttpPatch("{id:guid}/resolve")]
        public async Task<IActionResult> Resolve(Guid id, CancellationToken ct)
        {
            try
            {
                await _service.ResolveAsync(id, CurrentUserId, ct);
                return OkResponse<object>(null!, "Alerta resolvido");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }
    }
}
