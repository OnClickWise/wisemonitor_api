using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [Route("api/super-admin/agent")]
    public class SuperAdminAgentController : SuperAdminBaseController
    {
        private readonly ISuperAdminAgentService _service;

        public SuperAdminAgentController(ISuperAdminAgentService service)
            => _service = service;

        /// <summary>Lista todas as versões do agente Desktop publicadas.</summary>
        [HttpGet("versions")]
        public async Task<IActionResult> GetVersions(CancellationToken ct)
            => OkResponse(await _service.GetVersionsAsync(ct));

        /// <summary>Publica uma nova versão do agente.</summary>
        [HttpPost("versions")]
        public async Task<IActionResult> PublishVersion([FromBody] PublishAgentVersionDTO dto, CancellationToken ct)
        {
            try
            {
                var result = await _service.PublishVersionAsync(dto, CurrentUserId, ct);
                return CreatedResponse(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        /// <summary>Altera o canal de distribuição da versão (Stable/Beta/Alpha/Deprecated).</summary>
        [HttpPatch("versions/{id:guid}")]
        public async Task<IActionResult> UpdateChannel(Guid id, [FromBody] UpdateAgentChannelDTO dto, CancellationToken ct)
        {
            try
            {
                await _service.UpdateChannelAsync(id, dto.Channel, ct);
                return OkResponse<object>(null!, "Canal atualizado");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }

        /// <summary>Força atualização dos dispositivos para esta versão.</summary>
        [HttpPost("versions/{id:guid}/force-update")]
        public async Task<IActionResult> ForceUpdate(Guid id, [FromBody] ForceAgentUpdateDTO dto, CancellationToken ct)
        {
            try
            {
                var affected = await _service.ForceUpdateAsync(id, dto, ct);
                return OkResponse(new { AffectedDevices = affected });
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }

        /// <summary>Distribuição de versões do agente entre os dispositivos.</summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetVersionStats(CancellationToken ct)
            => OkResponse(await _service.GetVersionDistributionAsync(ct));
    }
}
