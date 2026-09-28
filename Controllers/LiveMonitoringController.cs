using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Text;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.Services;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models.Billing;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LiveMonitoringController : ControllerBase
    {
        private readonly LiveMonitoringService _liveService;
        private readonly IScreenshotService _screenshotService;
        private readonly IAccessScopeService _scopeService;
        private readonly IEntitlementService _entitlements;
        private readonly ILogger<LiveMonitoringController> _logger;

        public LiveMonitoringController(
            LiveMonitoringService liveService,
            IScreenshotService screenshotService,
            IAccessScopeService scopeService,
            IEntitlementService entitlements,
            ILogger<LiveMonitoringController> logger)
        {
            _liveService = liveService;
            _screenshotService = screenshotService;
            _scopeService = scopeService;
            _entitlements = entitlements;
            _logger = logger;
        }

        private Guid? GetOrgId()
        {
            var claim = User.FindFirst("orgId")?.Value
                     ?? User.FindFirst("organizationId")?.Value
                     ?? User.FindFirst("OrganizationId")?.Value;
            if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var id))
                return null;
            return id;
        }

        // Supervisor só pode ver os devices dos usuários das equipes que administra —
        // retorna null quando o papel não precisa de restrição (vê a org toda).
        private async Task<HashSet<string>?> GetAllowedUserIdsAsync()
        {
            var scope = await _scopeService.GetScopeAsync(User);
            return scope.IsRestricted
                ? scope.UserIds.Select(id => id.ToString()).ToHashSet()
                : null;
        }

        // ✅ Upload de Screenshot
        [HttpPost("screenshots")]
        [RequestSizeLimit(5 * 1024 * 1024)] // limite de 5MB
        public async Task<IActionResult> UploadScreenshot([FromForm] LiveMonitoringScreenshotUploadDTO dto)
        {
            if (dto == null || dto.Screenshot == null || dto.Screenshot.Length == 0)
                return BadRequest(new { message = "Arquivo inválido" });

            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
            var ext = Path.GetExtension(dto.Screenshot.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { message = "Formato de arquivo não suportado. Use PNG ou JPG." });

            // Screenshots são do Professional em diante — mesma regra do ScreenshotsController.
            if (!await _entitlements.HasFeatureAsync(dto.OrganizationId, Features.Screenshots, HttpContext.RequestAborted))
                return RequiresFeatureFilter.FeatureNotInPlan(Features.Screenshots);

            try
            {
                var result = await _screenshotService.SaveScreenshotAsync(dto);

                // Ajuste: usamos ScreenshotUrl como referência para Created()
                return Created(result.ScreenshotUrl, new
                {
                    message = "Screenshot salva com sucesso",
                    result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Screenshot Upload] Erro ao salvar screenshot");
                return StatusCode(500, new { message = "Erro ao salvar screenshot", error = ex.Message });
            }
        }

        // 🔹 Atualização de dispositivo
        [HttpPost("update")]
        public IActionResult UpdateDevice([FromBody] LiveDeviceUpdateDTO dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.DeviceId))
                return BadRequest(new { message = "Dados inválidos" });

            _liveService.RegisterOrUpdateDevice(dto);
            return Ok(new { message = "Dados do dispositivo atualizados" });
        }

        // 🔹 Lista todos os dispositivos da organização
        [HttpGet("devices")]
        [Authorize]
        public async Task<ActionResult<IReadOnlyCollection<MonitoringMessageDto>>> GetAllDevices()
        {
            var orgId = GetOrgId();
            if (orgId == null)
                return Forbid();

            var devices = _liveService.GetAllLiveDevices(orgId.Value.ToString());

            var allowedUserIds = await GetAllowedUserIdsAsync();
            if (allowedUserIds != null)
                devices = devices.Where(d => allowedUserIds.Contains(d.UserId ?? "")).ToList();

            return Ok(devices);
        }

        // 🔹 Retorna um dispositivo específico
        [HttpGet("devices/{deviceId}")]
        [Authorize]
        public async Task<ActionResult<MonitoringMessageDto>> GetDevice(string deviceId)
        {
            var orgId = GetOrgId();
            var device = _liveService.GetLiveDevice(deviceId);

            // Device de outra organização responde igual a inexistente.
            if (device == null || orgId == null || (device.OrgId ?? "default") != orgId.Value.ToString())
                return NotFound(new { message = "Dispositivo não encontrado" });

            // Supervisor: só máquinas de quem está nas equipes que administra.
            var allowedUserIds = await GetAllowedUserIdsAsync();
            if (allowedUserIds != null && !allowedUserIds.Contains(device.UserId ?? ""))
                return NotFound(new { message = "Dispositivo não encontrado" });

            return Ok(device);
        }

        // 🔹 SSE (stream contínuo)
        [HttpGet("sse")]
        [Authorize]
        public async Task GetSse(CancellationToken cancellationToken)
        {
            var orgId = GetOrgId();
            if (orgId == null)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            var orgIdStr = orgId.Value.ToString();
            var allowedUserIds = await GetAllowedUserIdsAsync();

            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            _logger.LogInformation("[SSE] Cliente conectado ao SSE | Org={OrgId}", orgIdStr);

            while (!cancellationToken.IsCancellationRequested)
            {
                var devices = _liveService.GetAllLiveDevices(orgIdStr);
                if (allowedUserIds != null)
                    devices = devices.Where(d => allowedUserIds.Contains(d.UserId ?? "")).ToList();

                var json = System.Text.Json.JsonSerializer.Serialize(devices);

                var message = $"data: {json}\n\n";
                var bytes = Encoding.UTF8.GetBytes(message);

                try
                {
                    await Response.Body.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SSE] Erro ao enviar dados");
                    break;
                }

                await Task.Delay(2000, cancellationToken);
            }

            _logger.LogInformation("[SSE] Conexão SSE finalizada");
        }

        // 🔹 Polling simples
        [HttpGet("polling")]
        [Authorize]
        public async Task<IActionResult> Polling()
        {
            var orgId = GetOrgId();
            if (orgId == null)
                return Forbid();

            var devices = _liveService.GetAllLiveDevices(orgId.Value.ToString());

            var allowedUserIds = await GetAllowedUserIdsAsync();
            if (allowedUserIds != null)
                devices = devices.Where(d => allowedUserIds.Contains(d.UserId ?? "")).ToList();

            return Ok(new
            {
                time = DateTime.UtcNow,
                devices
            });
        }
    }
}
