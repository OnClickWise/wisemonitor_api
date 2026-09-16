using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public interface IProductivityClassificationRepository
    {
        Task<IEnumerable<ProductivityClassification>> GetByTeamAsync(
            Guid organizationId,
            Guid teamId);

        Task SaveAsync(
            IEnumerable<ProductivityClassification> items);
    }
}
