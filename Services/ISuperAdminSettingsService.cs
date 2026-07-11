using System;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs.SuperAdmin;

namespace WiseMonitor.Api.Services
{
    public interface ISuperAdminSettingsService
    {
        Task<PlatformSettingsDTO> GetAsync(CancellationToken ct = default);
        Task UpdateAsync(PlatformSettingsDTO dto, Guid adminId, CancellationToken ct = default);
    }
}
