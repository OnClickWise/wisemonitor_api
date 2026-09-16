using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public interface IDeviceService
    {
        Task<Device> CreateDeviceAsync(Device device, Guid orgId);
        Task<Device?> GetDeviceByIdAsync(Guid id, Guid orgId);
        Task<IEnumerable<Device>> GetAllDevicesAsync(Guid orgId);
        Task<Device?> UpdateDeviceAsync(Device device, Guid orgId);
        Task<bool> DeleteDeviceAsync(Guid id, Guid orgId);

        // --- Presença ---
        Task<Device> RegisterHeartbeatAsync(DeviceHeartbeatDTO dto, Guid orgId, CancellationToken ct = default);
        Task<Device?> MarkOfflineAsync(DeviceOfflineDTO dto, Guid orgId, CancellationToken ct = default);
    }
}
