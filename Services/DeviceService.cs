using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly IDeviceRepository _deviceRepository;

        public DeviceService(IDeviceRepository deviceRepository)
        {
            _deviceRepository = deviceRepository;
        }

        public async Task<Device> CreateDeviceAsync(Device device, Guid orgId)
        {
            // Garante que o device sempre pertence ao OrgId correto
            device.OrganizationId = orgId;
            device.CreatedAt = DateTime.UtcNow;
            device.UpdatedAt = DateTime.UtcNow;

            return await _deviceRepository.CreateAsync(device);
        }

        public async Task<Device?> GetDeviceByIdAsync(Guid id, Guid orgId)
        {
            return await _deviceRepository.GetByIdAsync(id, orgId);
        }

        public async Task<IEnumerable<Device>> GetAllDevicesAsync(Guid orgId)
        {
            return await _deviceRepository.GetAllAsync(orgId);
        }

        public async Task<Device?> UpdateDeviceAsync(Device device, Guid orgId)
        {
            device.OrganizationId = orgId;
            device.UpdatedAt = DateTime.UtcNow;
            return await _deviceRepository.UpdateAsync(device);
        }

        public async Task<bool> DeleteDeviceAsync(Guid id, Guid orgId)
        {
            return await _deviceRepository.DeleteAsync(id, orgId);
        }
    }
}
