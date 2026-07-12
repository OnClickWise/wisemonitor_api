using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/video-segments")]
    [Authorize]
    public class VideoSegmentsController : ControllerBase
    {
        private readonly IVideoSegmentService _videoSegmentService;
        private readonly ILogger<VideoSegmentsController> _logger;

        public VideoSegmentsController(IVideoSegmentService videoSegmentService, ILogger<VideoSegmentsController> logger)
        {
            _videoSegmentService = videoSegmentService;
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
        // 📥 DOWNLOAD/STREAM POR ID (com suporte a Range para o <video> tag)
        // ============================
        [HttpGet("{id:guid}")]
        [AllowAnonymous] // consistente com ScreenshotsController.GetById — evita exigir header de auth pro <video>/<img>
        public async Task<IActionResult> GetById(Guid id)
        {
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
            [FromQuery] DateTime to)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
                return BadRequest(new { message = "deviceId é obrigatório." });

            if (to <= from)
                return BadRequest(new { message = "'to' deve ser posterior a 'from'." });

            var baseUrl = GetBaseUrl();
            var history = await _videoSegmentService.GetHistoryWithContextAsync(deviceId, from, to, baseUrl);

            return Ok(history);
        }

        private string GetBaseUrl()
        {
            var proto = Request.Headers["X-Forwarded-Proto"].ToString();
            if (string.IsNullOrEmpty(proto)) proto = Request.Scheme;
            return $"{proto}://{Request.Host}";
        }
    }
}
