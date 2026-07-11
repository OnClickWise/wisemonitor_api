using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public interface IScreenshotRepository
    {
        Task<Screenshot?> GetByIdAsync(Guid id);

        // 🔴 LEGADO (evitar em dashboard)
        Task<IEnumerable<Screenshot>> GetAllAsync();

        // ✅ MULTI-TENANT (USAR NO DASHBOARD)
        Task<IEnumerable<Screenshot>> GetAllByOrganizationAsync(Guid organizationId);

        Task<Screenshot?> GetLastByUserAsync(Guid monitoredUserId);

        Task DeletePreviousAsync(Guid monitoredUserId);

        Task UpsertAsync(Screenshot screenshot);
    }
}
