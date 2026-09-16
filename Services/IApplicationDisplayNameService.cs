using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public interface IApplicationDisplayNameService
    {
        Task<IEnumerable<ApplicationDisplayName>> GetAllAsync(
            Guid organizationId);

        Task<ApplicationDisplayName> SaveAsync(
            Guid organizationId,
            string applicationName,
            string displayName);
    }
}
