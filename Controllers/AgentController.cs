using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WiseMonitor.Api.Configs;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/agent")]
    public class AgentController : ControllerBase
    {
        private readonly IAgentService _agentService;
        private readonly IWebHostEnvironment _env;
        private readonly AgentSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;

        public AgentController(
            IAgentService agentService,
            IWebHostEnvironment env,
            IOptions<AgentSettings> settings,
            IHttpClientFactory httpClientFactory)
        {
            _agentService = agentService;
            _env = env;
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
        }

        // ====================================================
        // DOWNLOAD AGENT (retorna JSON com URL)
        // ====================================================

        [HttpGet("download")]
        public IActionResult Download()
        {
            var result = _agentService.GetDownload();
            return Ok(result);
        }

        // ====================================================
        // INSTALLER — chunked streaming (sem Content-Length)
        // Cloud Run tem limite 32MB para respostas buffered.
        // Sem Content-Length força chunked encoding sem limite.
        // ====================================================

        [HttpGet("installer")]
        public async Task Installer()
        {
            // Se DirectDownloadUrl estiver configurada, faz proxy da URL remota.
            // Isso evita precisar incluir o arquivo no container Docker.
            if (!string.IsNullOrWhiteSpace(_settings.DirectDownloadUrl))
            {
                var fileName = _settings.FileName.Length > 0
                    ? _settings.FileName
                    : "WiseMonitorAgent.zip";

                var http = _httpClientFactory.CreateClient();
                using var upstream = await http.GetAsync(
                    _settings.DirectDownloadUrl,
                    HttpCompletionOption.ResponseHeadersRead);

                if (!upstream.IsSuccessStatusCode)
                {
                    Response.StatusCode = (int)upstream.StatusCode;
                    await Response.WriteAsync("Erro ao obter arquivo remoto.");
                    return;
                }

                Response.ContentType = upstream.Content.Headers.ContentType?.ToString()
                    ?? "application/octet-stream";
                Response.Headers.Append("Content-Disposition",
                    $"attachment; filename=\"{fileName}\"");

                await upstream.Content.CopyToAsync(Response.Body);
                return;
            }

            // Fallback: serve arquivo do wwwroot local via chunked streaming
            var webRoot = _env.WebRootPath
                ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");

            var localFileName = !string.IsNullOrWhiteSpace(_settings.FileName)
                ? _settings.FileName
                : "WiseMonitorAgentSetup.exe";

            var filePath = Path.Combine(
                webRoot, "downloads", "agent",
                _settings.PlatformFolder,
                $"v{_settings.CurrentVersion}",
                localFileName);

            if (!System.IO.File.Exists(filePath))
            {
                Response.StatusCode = 404;
                await Response.WriteAsync($"Installer não encontrado: {filePath}");
                return;
            }

            var isZip = localFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
            Response.ContentType = isZip
                ? "application/zip"
                : "application/vnd.microsoft.portable-executable";
            Response.Headers.Append("Content-Disposition",
                $"attachment; filename=\"{localFileName}\"");
            // SEM Content-Length → chunked encoding → sem limite de 32 MB no Cloud Run

            using var fs = new FileStream(
                filePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, bufferSize: 65536, useAsync: true);

            await fs.CopyToAsync(Response.Body);
        }

        // ====================================================
        // VERSION
        // ====================================================

        [HttpGet("version")]
        public IActionResult Version()
        {
            var result = _agentService.GetVersion();
            return Ok(result);
        }
    }
}