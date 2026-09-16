using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;

namespace WiseMonitor.Api.Services
{
    public interface IProductivityClassificationService
    {
        Task<IEnumerable<ProductivityClassificationResponseDTO>> GetByTeamAsync(
            Guid organizationId,
            Guid teamId);

        Task SaveAsync(
            Guid organizationId,
            Guid teamId,
            ProductivityClassificationSaveDTO dto);
    }
}
