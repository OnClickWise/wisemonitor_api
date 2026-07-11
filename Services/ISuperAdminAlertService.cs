using System;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Utils;

namespace WiseMonitor.Api.Services
{
    public interface ISuperAdminAlertService
    {
        Task<PagedResult<AlertRuleResponseDTO>> GetRulesAsync(CancellationToken ct = default);
        Task<AlertRuleResponseDTO> CreateRuleAsync(CreateAlertRuleDTO dto, CancellationToken ct = default);
        Task UpdateRuleAsync(Guid id, UpdateAlertRuleDTO dto, CancellationToken ct = default);
        Task DeleteRuleAsync(Guid id, CancellationToken ct = default);
        Task<PagedResult<AlertHistoryResponseDTO>> GetHistoryAsync(PaginationDTO pagination, CancellationToken ct = default);
        Task ResolveAsync(Guid id, Guid resolvedByUserId, CancellationToken ct = default);
    }
}
