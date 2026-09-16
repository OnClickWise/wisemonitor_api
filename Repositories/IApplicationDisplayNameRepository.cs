using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public interface IApplicationDisplayNameRepository
    {
        Task<IEnumerable<ApplicationDisplayName>> GetByOrganizationAsync(
            Guid organizationId);

        Task<ApplicationDisplayName?> GetByApplicationNameAsync(
            Guid organizationId,
            string applicationName);

        Task<ApplicationDisplayName> SaveAsync(
            Guid organizationId,
            string applicationName,
            string displayName);
    }
}
