using System;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Utils;

namespace WiseMonitor.Api.Services
{
    public interface ISuperAdminTenantService
    {
        Task<PagedResult<TenantDTO>> GetAllAsync(TenantFilterDTO filter, CancellationToken ct = default);
        Task<TenantDTO?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<TenantDTO> CreateAsync(CreateTenantDTO dto, Guid createdByAdminId, CancellationToken ct = default);
        Task UpdateAsync(Guid id, UpdateTenantDTO dto, CancellationToken ct = default);
        Task DeleteAsync(Guid id, string reason, Guid adminId, CancellationToken ct = default);
        Task SuspendAsync(Guid id, SuspendTenantDTO dto, Guid adminId, CancellationToken ct = default);
        Task ActivateAsync(Guid id, Guid adminId, CancellationToken ct = default);
        Task UpdatePlanAsync(Guid id, UpdatePlanDTO dto, Guid adminId, CancellationToken ct = default);
        Task<TenantStatsDTO?> GetStatsAsync(Guid id, CancellationToken ct = default);
        Task<PagedResult<SuperAdminUserResponseDTO>> GetUsersAsync(Guid tenantId, PaginationDTO pagination, CancellationToken ct = default);
        Task<string> ResetAdminPasswordAsync(Guid tenantId, Guid adminId, CancellationToken ct = default);
        Task<string> GenerateImpersonationTokenAsync(Guid tenantId, Guid adminId, CancellationToken ct = default);
        Task<BrandingDTO?> GetBrandingAsync(Guid tenantId, CancellationToken ct = default);
        Task UpdateBrandingAsync(Guid tenantId, UpdateBrandingDTO dto, CancellationToken ct = default);
    }
}
