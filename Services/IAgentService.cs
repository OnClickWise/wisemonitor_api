using WiseMonitor.Api.DTOs;

namespace WiseMonitor.Api.Services
{
    public interface IAgentService
    {
        AgentDownloadDTO GetDownload();
        AgentVersionDTO GetVersion();
    }
}