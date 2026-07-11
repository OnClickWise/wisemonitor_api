using System;
using System.Threading.Tasks;

namespace WiseMonitor.Api.Services
{
    public interface ILiveSessionService
    {
        Task<string> GetOrCreateSessionForOrganizationAsync(Guid organizationId);
        Task<string> GetOrCreateSessionForUserAsync(Guid organizationId, Guid userId);
        Task EndSessionForOrganizationAsync(Guid organizationId);
        Task EndSessionForUserAsync(Guid organizationId, Guid userId);
        bool HasActiveSession(Guid organizationId);
        bool HasActiveSession(Guid organizationId, Guid userId);
    }
}
