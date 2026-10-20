using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Models.Billing;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers 
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ScreenshotsController : ControllerBase
    {
        private static readonly bool ScreenshotsEnabled =
            !string.Equals(Environment.GetEnvironmentVariable("SCREENSHOTS_ENABLED"), "false", StringComparison.OrdinalIgnoreCase);

        private readonly IScreenshotService _screenshotService;
        private readonly ILiveMonitoringService _liveService;
        private readonly IDeviceService _deviceService;
        private readonly IEntitlementService _entitlements;
        private readonly IAccessScopeService _scopeService;
        private readonly ILogger<ScreenshotsController> _logger;

        public ScreenshotsController(
            IScreenshotService screenshotService,
            ILiveMonitoringService liveService,
            IDeviceService deviceService,
            IEntitlementService entitlements,
            IAccessScopeService scopeService,
            ILogger<ScreenshotsController> logger)
        {
            _screenshotService = screenshotService;
            _liveService = liveService;
            _deviceService = deviceService;
            _entitlements = entitlements;
            _scopeService = scopeService;
            _logger = logger;
        }

        // ============================
        // 📸 UPLOAD (multipart/form-data)
        // ============================
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(5 * 1024 * 1024)] // Limite de 5MB
        public async Task<IActionResult> Upload([FromForm] LiveMonitoringScreenshotUploadDTO dto)
        {
            // SCREENSHOTS_ENABLED=false desliga as capturas no servidor: o agent continua
            // mandando uma por minuto de cada máquina, e cada uma grava a imagem no banco e
            // reenvia a lista de devices a todos os painéis. Responde 204 — o agent trata
            // como sucesso — sem tocar no banco.
            if (!ScreenshotsEnabled)
                return NoContent();

            if (dto == null || dto.Screenshot == null || dto.Screenshot.Length == 0)
                return BadRequest(new { message = "Arquivo inválido." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg" };
            var ext = Path.GetExtension(dto.Screenshot.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { message = "Formato não suportado." });

            try
            {
                // 🔐 A organização vem sempre do token. Antes o upload era anônimo e
                // aceitava o OrganizationId do próprio formulário — qualquer pessoa
                // injetava capturas falsas no painel de qualquer empresa.
                dto.OrganizationId = User.GetOrganizationId();

                // Screenshots são do Professional em diante — não armazena para planos sem a feature.
                if (!await _entitlements.HasFeatureAsync(dto.OrganizationId, Features.Screenshots, HttpContext.RequestAborted))
                    return RequiresFeatureFilter.FeatureNotInPlan(Features.Screenshots);

                // Device já visto ao vivo em outra organização: não deixa sobrescrever
                // a miniatura/status do painel de outra empresa com o mesmo DeviceId.
                var liveEntry = _liveService.GetLiveDevice(dto.DeviceId);
                if (liveEntry != null && liveEntry.OrgId != dto.OrganizationId.ToString())
                    return NotFound();

                var result = await _screenshotService.SaveScreenshotAsync(dto);

                // Correção manual de um admin (Device.UserId) tem prioridade sobre quem
                // o próprio agent diz estar monitorando — é exatamente pra cobrir o caso
                // de alguém ter configurado o agent com a pessoa errada na máquina.
                Guid? manuallyAssignedUserId = null;
                if (Guid.TryParse(dto.DeviceId, out var deviceGuid))
                {
                    var device = await _deviceService.GetDeviceByIdAsync(deviceGuid, dto.OrganizationId);
                    manuallyAssignedUserId = device?.UserId;
                }
                var effectiveUserId = manuallyAssignedUserId ?? dto.MonitoredUserId;

                // Update live monitoring so the dashboard reflects this device immediately
                var proto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
                var baseUrl = $"{proto}://{Request.Host}";
                _liveService.RegisterOrUpdateDevice(new LiveDeviceUpdateDTO
                {
                    DeviceId    = dto.DeviceId,
                    OrgId       = dto.OrganizationId.ToString(),
                    UserId      = effectiveUserId.ToString(),
                    Username    = effectiveUserId.ToString(),
                    Department  = "—",
                    Status      = "online",
                    ThumbnailUrl  = $"{baseUrl}{result.ScreenshotUrl}",
                    FullScreenUrl = $"{baseUrl}{result.ScreenshotUrl}",
                    Timestamp   = DateTime.UtcNow
                });

                return Created(result.ScreenshotUrl, new { message = "Salvo com sucesso", result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Upload Erro]");
                return StatusCode(500, new { message = "Erro ao salvar" });
            }
        }

        // ============================
        // 📄 LISTAR POR ORGANIZAÇÃO
        // ============================
        [HttpGet("list")]
        [RequiresFeature(Features.Screenshots)]
        [Authorize] 
        public async Task<IActionResult> List()
        {
            // 1. Validação de Segurança (Debug + Lógica)
            var orgClaim = User.Claims.FirstOrDefault(c =>
                c.Type == "organizationId" || 
                c.Type == "OrganizationId" || 
                c.Type == "orgId");

            if (orgClaim == null)
                return Unauthorized("OrganizationId não encontrado.");

            var organizationId = Guid.Parse(orgClaim.Value);

            // 2. Busca e Retorno dos dados
            try 
            {
                var shots = await _screenshotService.GetAllByOrganizationAsync(organizationId);

                // Supervisor: só máquinas de quem está nas equipes que administra. Usa o
                // usuário efetivo da máquina (atribuição manual tem prioridade).
                var scope = await _scopeService.GetScopeAsync(User);
                if (scope.IsRestricted)
                {
                    var assigned = await _scopeService.GetAssignedDeviceUsersAsync(organizationId);
                    shots = shots
                        .Where(s => scope.CanAccessUser(
                            s.DeviceId != null && assigned.TryGetValue(s.DeviceId, out var owner)
                                ? owner
                                : s.MonitoredUserId))
                        .ToList();
                }

                var proto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
                var baseUrl = $"{proto}://{Request.Host}";

                return Ok(shots.Select(x => new
                {
                    id = x.Id,
                    monitoredUserId = x.MonitoredUserId,
                    deviceId = x.DeviceId,
                    capturedAt = x.CapturedAt,
                    createdAt = x.CapturedAt,
                    imageUrl = $"{baseUrl}{_screenshotService.BuildSignedPath(x.Id)}"
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Screenshots] Erro ao listar");
                return StatusCode(500, new { error = "Erro interno ao listar" });
            }
        }

        // ============================
        // 🖼️ ÚLTIMA SCREENSHOT DO USUÁRIO
        // ============================
        [HttpGet("last/{userId:guid}")]
        [RequiresFeature(Features.Screenshots)]
        public async Task<IActionResult> GetLast(Guid userId)
        {
            // 🔒 Usuário da mesma organização e dentro do escopo do caller
            // (supervisor: só membros das equipes que administra)
            if (!await _scopeService.CanAccessUserAsync(User, userId))
                return Forbid();

            var screenshot = await _screenshotService.GetLastScreenshotByUserAsync(userId);

            if (screenshot == null)
                return NotFound();

            return Ok(screenshot);
        }

        // ============================
        // 📥 DOWNLOAD POR ID
        // ============================
        [HttpGet("{id:guid}")]
        [AllowAnonymous] // a tag <img> do navegador não manda header de autenticação
        public async Task<IActionResult> GetById(Guid id, [FromQuery] long exp, [FromQuery] string? sig)
        {
            // Única barreira desta rota: o filtro de tenant do EF não vale para requisição
            // anônima, então sem a assinatura qualquer GUID abria a tela de outra empresa.
            if (!_screenshotService.ValidateAccess(id, exp, sig))
                return Unauthorized();

            var screenshot = await _screenshotService.GetByIdAsync(id);
            if (screenshot == null)
                return NotFound();

            // Cada captura tem id próprio e nunca é alterada: o navegador pode reaproveitar
            // em vez de baixar de novo a cada refresh das telas de dispositivos. "private"
            // para proxies/CDNs não guardarem imagens de tela dos clientes.
            Response.Headers.CacheControl = "private, max-age=86400, immutable";

            return File(
                screenshot.ImageData,
                screenshot.ContentType ?? "image/png",
                screenshot.FileName
            );
        }
    }
}