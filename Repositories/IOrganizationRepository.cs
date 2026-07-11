using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public interface IOrganizationRepository
    {
        Task<Organization?> GetOrganizationWithAdminAsync(Guid organizationId);
        Task<Organization?> GetByIdWithAdminAsync(Guid id);
        Task<Organization?> GetByIdAsync(Guid id);
        Task UpdateAsync(Organization organization);
    }
}