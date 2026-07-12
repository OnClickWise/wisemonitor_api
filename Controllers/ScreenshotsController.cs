using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers 
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScreenshotsController : ControllerBase
    {
        private readonly IScreenshotService _screenshotService;
        private readonly ILiveMonitoringService _liveService;
        private readonly ILogger<ScreenshotsController> _logger;

        public ScreenshotsController(
            IScreenshotService screenshotService,
            ILiveMonitoringService liveService,
            ILogger<ScreenshotsController> logger)
        {
            _screenshotService = screenshotService;
            _liveService = liveService;
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
                // 🔐 Segurança: Tenta pegar OrgId do Token se disponível
                // Se for upload via Agent sem token de usuário, o DTO já deve ter os dados
                // Se for upload autenticado, preenchemos aqui
                if (User.Identity.IsAuthenticated)
                {
                    dto.OrganizationId = User.GetOrganizationId();
                }

                var result = await _screenshotService.SaveScreenshotAsync(dto);

                // Update live monitoring so the dashboard reflects this device immediately
                var proto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
                var baseUrl = $"{proto}://{Request.Host}";
                _liveService.RegisterOrUpdateDevice(new LiveDeviceUpdateDTO
                {
                    DeviceId    = dto.DeviceId,
                    OrgId       = dto.OrganizationId.ToString(),
                    Username    = dto.MonitoredUserId.ToString(),
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
                return StatusCode(500, new { message = "Erro ao salvar", error = ex.Message });
            }
        }

        // ============================
        // 📄 LISTAR POR ORGANIZAÇÃO
        // ============================
        [HttpGet("list")]
        [Authorize] 
        public async Task<IActionResult> List()
        {
            // 1. Validação de Segurança (Debug + Lógica)
            var orgClaim = User.Claims.FirstOrDefault(c =>
                c.Type == "organizationId" || 
                c.Type == "OrganizationId" || 
                c.Type == "orgId");

            if (orgClaim == null)
            {
                var claimList = string.Join(", ", User.Claims.Select(c => c.Type));
                return Unauthorized($"OrganizationId não encontrado. Chaves disponíveis: {claimList}");
            }

            var organizationId = Guid.Parse(orgClaim.Value);

            // 2. Busca e Retorno dos dados
            try 
            {
                var shots = await _screenshotService.GetAllByOrganizationAsync(organizationId);
                var proto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme;
                var baseUrl = $"{proto}://{Request.Host}";

                return Ok(shots.Select(x => new
                {
                    id = x.Id,
                    monitoredUserId = x.MonitoredUserId,
                    deviceId = x.DeviceId,
                    capturedAt = x.CapturedAt,
                    createdAt = x.CapturedAt,
                    imageUrl = $"{baseUrl}/api/Screenshots/{x.Id}"
                }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Erro interno ao listar", detail = ex.Message });
            }
        }

        // ============================
        // 🖼️ ÚLTIMA SCREENSHOT DO USUÁRIO
        // ============================
        [HttpGet("last/{userId:guid}")]
        public async Task<IActionResult> GetLast(Guid userId)
        {
            var screenshot = await _screenshotService.GetLastScreenshotByUserAsync(userId);

            if (screenshot == null)
                return NotFound();

            return Ok(screenshot);
        }

        // ============================
        // 📥 DOWNLOAD POR ID
        // ============================
        [HttpGet("{id:guid}")]
        [AllowAnonymous] // <--- Importante para carregar imagens no frontend sem header de auth
        public async Task<IActionResult> GetById(Guid id)
        {
            var screenshot = await _screenshotService.GetByIdAsync(id);
            if (screenshot == null)
                return NotFound();

            return File(
                screenshot.ImageData,
                screenshot.ContentType ?? "image/png",
                screenshot.FileName
            );
        }
    }
}