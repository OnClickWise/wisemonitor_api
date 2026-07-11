using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [Route("api/super-admin/integrations")]
    public class SuperAdminIntegrationsController : SuperAdminBaseController
    {
        private readonly ISuperAdminIntegrationService _service;

        public SuperAdminIntegrationsController(ISuperAdminIntegrationService service)
            => _service = service;

        /// <summary>Lista todas as integrações configuradas na plataforma.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
            => OkResponse(await _service.GetAllAsync(ct));

        /// <summary>Cria uma nova integração (Slack, SMTP, Webhook, etc.).</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateIntegrationDTO dto, CancellationToken ct)
        {
            var result = await _service.CreateAsync(dto, ct);
            return CreatedResponse(result);
        }

        /// <summary>Atualiza configurações da integração.</summary>
        [HttpPatch("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateIntegrationDTO dto, CancellationToken ct)
        {
            try
            {
                await _service.UpdateAsync(id, dto, ct);
                return OkResponse<object>(null!, "Integração atualizada");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }

        /// <summary>Remove a integração.</summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                await _service.DeleteAsync(id, ct);
                return OkResponse<object>(null!, "Integração removida");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }

        /// <summary>Testa conectividade da integração.</summary>
        [HttpPost("{id:guid}/test")]
        public async Task<IActionResult> Test(Guid id, CancellationToken ct)
        {
            try
            {
                var result = await _service.TestConnectivityAsync(id, ct);
                return OkResponse(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }
    }
}
