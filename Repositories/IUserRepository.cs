using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public interface IUserRepository
    {
        Task<User> CreateAsync(User user);
        Task<IEnumerable<User>> GetAllAsync(Guid organizationId);
        Task<User?> GetByIdAsync(Guid id, Guid organizationId);
        Task<bool> UpdateAsync(User user);
        Task<bool> DeleteAsync(Guid id, Guid organizationId);
    }
}
