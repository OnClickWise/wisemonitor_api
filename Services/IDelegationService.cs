using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs.Delegation;

namespace WiseMonitor.Api.Services
{
    public interface IDelegationService
    {
        Task<DelegationDTO> CreateAsync(CreateDelegationDTO dto, Guid organizationId, Guid delegatorId);
        Task<List<DelegationDTO>> GetAllAsync(Guid organizationId);
        Task<List<DelegationDTO>> GetByUserAsync(Guid userId, Guid organizationId);
        Task RevokeAsync(Guid delegationId, Guid organizationId, Guid revokedBy);
        Task ExpireOutdatedAsync();
    }
}
