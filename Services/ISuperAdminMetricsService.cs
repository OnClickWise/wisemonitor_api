using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs.SuperAdmin;

namespace WiseMonitor.Api.Services
{
    public interface ISuperAdminMetricsService
    {
        Task<PlatformOverviewDTO> GetOverviewAsync(CancellationToken ct = default);
        Task<IEnumerable<TimeseriesPointDTO>> GetTimeseriesAsync(TimeseriesQueryDTO query, CancellationToken ct = default);
        Task<IEnumerable<PlanDistributionItemDTO>> GetPlanDistributionAsync(CancellationToken ct = default);
        Task<SystemHealthDTO> GetSystemHealthAsync(CancellationToken ct = default);
    }
}
