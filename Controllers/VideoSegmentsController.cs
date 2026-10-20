using System;
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
    [Route("api/video-segments")]
    [RequiresFeature(Features.LiveStream)]
    [Authorize]
    public class VideoSegmentsController : ControllerBase
    {
        private readonly IVideoSegmentService _videoSegmentService;
        private readonly IAccessScopeService _scopeService;
        private readonly ILogger<VideoSegmentsController> _logger;

        public VideoSegmentsController(
            IVideoSegmentService videoSegmentService,
            IAccessScopeService scopeService,
            ILogger<VideoSegmentsController> logger)
        {
            _videoSegmentService = videoSegmentService;
            _scopeService = scopeService;
            _logger = logger;
        }

        // ============================
        // 📤 UPLOAD DE SEGMENTO (desktop agent)
        // ============================
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10 * 1024 * 1024)] // 10MB — segmento de ~10s em baixa resolução/fps deve ficar bem abaixo disso
        public async Task<IActionResult> Upload([FromForm] VideoSegmentUploadDTO dto)
        {
            if (dto == null || dto.Segment == null || dto.Segment.Length == 0)
                return BadRequest(new { message = "Segmento de vídeo inválido." });

            try
            {
                await _videoSegmentService.SaveSegmentAsync(dto);
                return Ok(new { message = "Segmento salvo com sucesso." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VideoSegments] Erro ao salvar segmento");
                return StatusCode(500, new { message = "Erro ao salvar segmento." });
            }
        }

        // ============================
        // ☁️ UPLOAD DIRETO AO CLOUD STORAGE — 1) pede a URL, 2) PUT no bucket, 3) confirma
        // ============================
        // O MP4 não passa pela API nem pelo Postgres. 204 = bucket desligado no servidor
        // (VIDEO_BUCKET vazio): o agent volta ao upload antigo (multipart em /upload).
        [HttpPost("upload-url")]
        public async Task<IActionResult> CreateUploadUrl()
        {
            var orgId = User.GetOrganizationId();
            if (orgId == Guid.Empty)
                return Forbid();

            var ticket = await _videoSegmentService.CreateUploadTicketAsync(orgId);
            if (ticket == null)
                return NoContent();

            return Ok(ticket);
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> ConfirmUpload([FromBody] VideoSegmentConfirmDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (dto.EndedAt < dto.StartedAt)
                return BadRequest(new { message = "'EndedAt' deve ser posterior a 'StartedAt'." });

            var orgId = User.GetOrganizationId();
            if (orgId == Guid.Empty)
                return Forbid();

            try
            {
                var result = await _videoSegmentService.ConfirmUploadAsync(orgId, dto, GetBaseUrl());
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ============================
        // 📥 DOWNLOAD/STREAM POR ID (com suporte a Range para o <video> tag)
        // ============================
        [HttpGet("{id:guid}")]
        [AllowAnonymous] // a tag <video> do navegador não manda header de autenticação
        public async Task<IActionResult> GetById(Guid id, [FromQuery] long exp, [FromQuery] string? sig)
        {
            // Sem isso, qualquer pessoa — mesmo sem login — sabendo ou adivinhando o GUID
            // acessaria o vídeo de outro tenant, já que essa rota não pode exigir o Bearer
            // normal (ver comentário do AllowAnonymous acima) e o filtro de tenant do EF
            // não se aplica a requisições anônimas.
            if (!_videoSegmentService.ValidateAccess(id, exp, sig))
                return Unauthorized();

            // Segmento no bucket: o navegador segue o redirect e baixa direto do Cloud
            // Storage (com suporte a Range), sem os bytes passarem pela API.
            var metadata = await _videoSegmentService.GetMetadataByIdAsync(id);
            if (metadata == null)
                return NotFound();

            if (metadata.StorageKey != null)
                return Redirect(await _videoSegmentService.CreateReadUrlAsync(metadata.StorageKey));

            var segment = await _videoSegmentService.GetByIdAsync(id);
            if (segment == null)
                return NotFound();

            return File(segment.VideoData, segment.ContentType, enableRangeProcessing: true);
        }

        // ============================
        // 🔴 SEGMENTO MAIS RECENTE (entrada da visualização "ao vivo")
        // ============================
        [HttpGet("latest")]
        public async Task<IActionResult> GetLatest([FromQuery] string deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return BadRequest(new { message = "deviceId é obrigatório." });

            // Supervisor: só máquinas de quem está nas equipes que administra.
            if (!await _scopeService.CanAccessDeviceAsync(User, deviceId))
                return NotFound();

            var baseUrl = GetBaseUrl();
            var latest = await _videoSegmentService.GetLatestAsync(deviceId, baseUrl);

            return latest == null ? NotFound() : Ok(latest);
        }

        // ============================
        // 🕘 HISTÓRICO (com contexto de app-focus/teclado correlacionado)
        // ============================
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] string deviceId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] Guid? userId)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return BadRequest(new { message = "deviceId é obrigatório." });

            if (to <= from)
                return BadRequest(new { message = "'to' deve ser posterior a 'from'." });

            if (!await _scopeService.CanAccessDeviceAsync(User, deviceId))
                return NotFound();

            if (userId.HasValue && !await _scopeService.CanAccessUserAsync(User, userId.Value))
                return NotFound();

            var baseUrl = GetBaseUrl();
            var history = await _videoSegmentService.GetHistoryWithContextAsync(deviceId, from, to, baseUrl, userId);

            return Ok(history);
        }

        // ============================
        // 🎬 VÍDEO COMPLETO DE UMA ATIVIDADE (concatena os segmentos via ffmpeg)
        // ============================
        [HttpGet("activity-video")]
        public async Task<IActionResult> GetActivityVideo(
            [FromQuery] string deviceId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return BadRequest(new { message = "deviceId é obrigatório." });

            if (to <= from)
                return BadRequest(new { message = "'to' deve ser posterior a 'from'." });

            if (!await _scopeService.CanAccessDeviceAsync(User, deviceId))
                return NotFound();

            try
            {
                var result = await _videoSegmentService.GetActivityVideoAsync(deviceId, from, to);
                if (result == null)
                    return NotFound(new { message = "Nenhum vídeo foi encontrado para essa atividade." });

                var fileName = $"atividade_{deviceId}_{from:yyyyMMdd_HHmmss}.mp4";
                return File(result.VideoData, result.ContentType, fileName, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VideoSegments] Erro ao gerar vídeo de atividade");
                return StatusCode(500, new { message = "Erro ao gerar vídeo de atividade." });
            }
        }

        private string GetBaseUrl()
        {
            var proto = Request.Headers["X-Forwarded-Proto"].ToString();
            if (string.IsNullOrEmpty(proto)) proto = Request.Scheme;
            return $"{proto}://{Request.Host}";
        }
    }
}
