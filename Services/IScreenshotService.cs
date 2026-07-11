using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public interface IScreenshotService
    {
        Task<LiveMonitoringScreenshotResponseDTO> SaveScreenshotAsync(
            LiveMonitoringScreenshotUploadDTO dto);

        Task<LastScreenshotDTO?> GetLastScreenshotByUserAsync(Guid monitoredUserId);

        // 🔴 LEGADO (NÃO USAR EM DASHBOARD)
        Task<IEnumerable<Screenshot>> GetAllAsync();

        // ✅ CORRETO (MULTI-TENANT)
        Task<IEnumerable<Screenshot>> GetAllByOrganizationAsync(Guid organizationId);

        Task<Screenshot?> GetByIdAsync(Guid id);
    }
}
