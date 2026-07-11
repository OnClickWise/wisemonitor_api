using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs.SuperAdmin;

namespace WiseMonitor.Api.Services
{
    public interface ISuperAdminIntegrationService
    {
        Task<IEnumerable<IntegrationResponseDTO>> GetAllAsync(CancellationToken ct = default);
        Task<IntegrationResponseDTO> CreateAsync(CreateIntegrationDTO dto, CancellationToken ct = default);
        Task UpdateAsync(Guid id, UpdateIntegrationDTO dto, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
        Task<IntegrationTestResultDTO> TestConnectivityAsync(Guid id, CancellationToken ct = default);
    }
}
