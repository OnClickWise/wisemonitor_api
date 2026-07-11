using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [Route("api/super-admin/settings")]
    public class SuperAdminSettingsController : SuperAdminBaseController
    {
        private readonly ISuperAdminSettingsService _service;

        public SuperAdminSettingsController(ISuperAdminSettingsService service)
            => _service = service;

        /// <summary>Retorna as configurações globais da plataforma.</summary>
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
            => OkResponse(await _service.GetAsync(ct));

        /// <summary>Atualiza as configurações globais da plataforma.</summary>
        [HttpPatch]
        public async Task<IActionResult> Update([FromBody] PlatformSettingsDTO dto, CancellationToken ct)
        {
            await _service.UpdateAsync(dto, CurrentUserId, ct);
            return OkResponse<object>(null!, "Configurações salvas com sucesso");
        }
    }
}
