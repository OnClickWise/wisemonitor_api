using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs.SuperAdmin;

namespace WiseMonitor.Api.Services
{
    public interface ISuperAdminAgentService
    {
        Task<IEnumerable<AgentVersionResponseDTO>> GetVersionsAsync(CancellationToken ct = default);
        Task<AgentVersionResponseDTO> PublishVersionAsync(PublishAgentVersionDTO dto, Guid adminId, CancellationToken ct = default);
        Task UpdateChannelAsync(Guid id, string channel, CancellationToken ct = default);
        Task<int> ForceUpdateAsync(Guid versionId, ForceAgentUpdateDTO dto, CancellationToken ct = default);
        Task<IEnumerable<AgentVersionDistributionDTO>> GetVersionDistributionAsync(CancellationToken ct = default);
    }
}
