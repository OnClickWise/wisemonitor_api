using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Utils;

namespace WiseMonitor.Api.Services
{
    public interface ISuperAdminUserService
    {
        Task<PagedResult<SuperAdminUserResponseDTO>> GetAllAsync(SuperAdminUserFilterDTO filter, CancellationToken ct = default);
        Task<SuperAdminUserResponseDTO?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<SuperAdminUserResponseDTO> CreateSuperAdminAsync(CreateSuperAdminDTO dto, Guid creatorId, CancellationToken ct = default);
        Task UpdateAsync(Guid id, UserUpdateDTO dto, CancellationToken ct = default);
        Task DeleteAsync(Guid id, Guid adminId, CancellationToken ct = default);
        Task InvalidateAllSessionsAsync(Guid id, CancellationToken ct = default);
        Task UnlockAsync(Guid id, Guid adminId, CancellationToken ct = default);
        Task<IEnumerable<object>> GetActiveSessionsAsync(Guid id, CancellationToken ct = default);
    }
}
