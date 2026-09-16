using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public interface IDeviceRepository
    {
        Task<Device> CreateAsync(Device device);
        Task<Device?> GetByIdAsync(Guid id, Guid orgId);
        Task<Device?> GetByHostnameAsync(string hostname, Guid orgId);
        Task<IEnumerable<Device>> GetAllAsync(Guid orgId);
        Task<Device?> UpdateAsync(Device device);
        Task<bool> DeleteAsync(Guid id, Guid orgId);

        /// <summary>
        /// Marca como offline todo device cujo último heartbeat é anterior a
        /// <paramref name="threshold"/>. Cobre os desligamentos em que o agent não
        /// consegue avisar (queda de energia, cabo removido, processo morto).
        /// Retorna quantos foram afetados.
        /// </summary>
        Task<int> MarkStaleOfflineAsync(DateTime threshold, CancellationToken ct = default);
    }
}
