using Microsoft.Extensions.Options;
using WiseMonitor.Api.Configs;
using WiseMonitor.Api.DTOs;

namespace WiseMonitor.Api.Services
{
    public class AgentService : IAgentService
    {
        private readonly AgentSettings _settings;

        public AgentService(
            IOptions<AgentSettings> settings)
        {
            _settings =
                settings.Value;
        }

        // ====================================================
        // DOWNLOAD
        // ====================================================

        public AgentDownloadDTO GetDownload()
        {
            // Prefere URL direta (GitHub LFS, GCS, etc.) se configurada;
            // senão cai no endpoint de streaming local /installer.
            var url = !string.IsNullOrWhiteSpace(_settings.DirectDownloadUrl)
                ? _settings.DirectDownloadUrl
                : $"{_settings.AppBaseUrl}/api/agent/installer";

            return new AgentDownloadDTO
            {
                Url = url,
                Version = _settings.CurrentVersion,
                FileName = _settings.FileName,
                Platform = _settings.PlatformFolder
            };
        }

        // ====================================================
        // VERSION
        // ====================================================

        public AgentVersionDTO GetVersion()
        {
            var downloadUrl =
                $"{_settings.BaseDownloadUrl}/" +
                $"{_settings.PlatformFolder}/" +
                $"v{_settings.CurrentVersion}/" +
                $"{_settings.FileName}";

            return new AgentVersionDTO
            {
                Version =
                    _settings.CurrentVersion,

                ForceUpdate =
                    _settings.ForceUpdate,

                DownloadUrl =
                    downloadUrl
            };
        }
    }
}