using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public interface IDeviceRepository
    {
        Task<Device> CreateAsync(Device device);
        Task<Device?> GetByIdAsync(Guid id, Guid orgId);
        Task<IEnumerable<Device>> GetAllAsync(Guid orgId);
        Task<Device?> UpdateAsync(Device device);
        Task<bool> DeleteAsync(Guid id, Guid orgId);
    }
}
