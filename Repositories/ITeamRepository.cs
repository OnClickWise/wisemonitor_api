using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public interface ITeamRepository
    {
        Task CreateAsync(Team team);
        Task<Team?> GetByIdAsync(Guid id, Guid organizationId);
        Task<List<Team>> GetAllAsync(Guid organizationId);
        Task UpdateAsync(Team team);
        Task DeleteAsync(Team team);
    }
}
