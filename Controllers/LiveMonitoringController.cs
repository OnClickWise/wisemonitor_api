using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Text;
using WiseMonitor.Api.Services;
using WiseMonitor.Api.DTOs;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LiveMonitoringController : ControllerBase
    {
        private readonly LiveMonitoringService _liveService;
        private readonly IScreenshotService _screenshotService;

        public LiveMonitoringController(
            LiveMonitoringService liveService,
            IScreenshotService screenshotService)
        {
            _liveService = liveService;
            _screenshotService = screenshotService;
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
                Console.WriteLine($"[Screenshot Upload] Erro: {ex.Message}");
                return StatusCode(500, new { message = "Erro ao salvar screenshot", error = ex.Message });
            }
        }

        // ✅ Frame de transmissão ao vivo (não persiste — apenas broadcast via /ws/monitor)
        [HttpPost("frame")]
        [RequestSizeLimit(1_500_000)] // ~1.5MB, frames JPEG comprimidos
        public IActionResult UploadFrame([FromForm] LiveFrameUploadDTO dto)
        {
            if (dto == null || dto.Frame == null || dto.Frame.Length == 0)
                return BadRequest(new { message = "Frame inválido" });

            try
            {
                using var ms = new MemoryStream();
                dto.Frame.CopyTo(ms);
                var contentType = string.IsNullOrWhiteSpace(dto.Frame.ContentType) ? "image/jpeg" : dto.Frame.ContentType;
                _liveService.UpdateDeviceScreen(dto.DeviceId, dto.OrganizationId.ToString(), ms.ToArray(), contentType);

                return Ok(new { message = "Frame recebido" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Live Frame] Erro: {ex.Message}");
                return StatusCode(500, new { message = "Erro ao processar frame", error = ex.Message });
            }
        }

        // 🔹 Indica se algum admin está assistindo esse device agora (usado pelo Desktop)
        [HttpGet("devices/{deviceId}/watched")]
        public IActionResult IsWatched(string deviceId)
        {
            return Ok(new { watched = _liveService.IsWatched(deviceId) });
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

        // 🔹 Lista todos os dispositivos
        [HttpGet("devices")]
        public ActionResult<IReadOnlyCollection<MonitoringMessageDto>> GetAllDevices()
        {
            var devices = _liveService.GetAllLiveDevices();
            return Ok(devices);
        }

        // 🔹 Retorna um dispositivo específico
        [HttpGet("devices/{deviceId}")]
        public ActionResult<MonitoringMessageDto> GetDevice(string deviceId)
        {
            var device = _liveService.GetLiveDevice(deviceId);
            if (device == null)
                return NotFound(new { message = "Dispositivo não encontrado" });

            return Ok(device);
        }

        // 🔹 SSE (stream contínuo)
        [HttpGet("sse")]
        public async Task GetSse(CancellationToken cancellationToken)
        {
            Response.Headers.Append("Content-Type", "text/event-stream");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("Connection", "keep-alive");

            Console.WriteLine("[SSE] Cliente conectado ao SSE");

            while (!cancellationToken.IsCancellationRequested)
            {
                var devices = _liveService.GetAllLiveDevices();
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
                    Console.WriteLine($"[SSE] Erro ao enviar dados: {ex.Message}");
                    break;
                }

                await Task.Delay(2000, cancellationToken);
            }

            Console.WriteLine("[SSE] Conexão SSE finalizada");
        }

        // 🔹 Polling simples
        [HttpGet("polling")]
        public IActionResult Polling()
        {
            var devices = _liveService.GetAllLiveDevices();
            return Ok(new
            {
                time = DateTime.UtcNow,
                devices
            });
        }
    }
}
