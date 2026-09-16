using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
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

        // ====================================================
        // PRESENÇA
        // ====================================================

        public async Task<Device> RegisterHeartbeatAsync(
            DeviceHeartbeatDTO dto, Guid orgId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            // Resolve por Id e, se não achar, por hostname. O fallback por hostname
            // é o que impede a máquina de virar um device novo a cada reinstalação
            // ou perda do session.json — sem ele o device antigo ficaria online para
            // sempre, já que nada volta a tocar naquela linha.
            var device = await _deviceRepository.GetByIdAsync(dto.DeviceId, orgId);

            if (device == null && !string.IsNullOrWhiteSpace(dto.Hostname))
                device = await _deviceRepository.GetByHostnameAsync(dto.Hostname, orgId);

            if (device == null)
            {
                device = new Device
                {
                    Id = dto.DeviceId == Guid.Empty ? Guid.NewGuid() : dto.DeviceId,
                    OrganizationId = orgId,
                    Hostname = dto.Hostname ?? "Desktop Agent",
                    IpAddress = dto.IpAddress ?? string.Empty,
                    AgentVersion = dto.AgentVersion,
                    IsOnline = true,
                    LastSeen = now,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                return await _deviceRepository.CreateAsync(device);
            }

            device.IsOnline = true;
            device.LastSeen = now;
            device.UpdatedAt = now;

            if (!string.IsNullOrWhiteSpace(dto.Hostname)) device.Hostname = dto.Hostname;
            if (!string.IsNullOrWhiteSpace(dto.IpAddress)) device.IpAddress = dto.IpAddress;
            if (!string.IsNullOrWhiteSpace(dto.AgentVersion)) device.AgentVersion = dto.AgentVersion;

            return await _deviceRepository.UpdateAsync(device) ?? device;
        }

        public async Task<Device?> MarkOfflineAsync(
            DeviceOfflineDTO dto, Guid orgId, CancellationToken ct = default)
        {
            var device = await _deviceRepository.GetByIdAsync(dto.DeviceId, orgId);

            if (device == null && !string.IsNullOrWhiteSpace(dto.Hostname))
                device = await _deviceRepository.GetByHostnameAsync(dto.Hostname, orgId);

            if (device == null)
                return null;

            device.IsOnline = false;
            device.UpdatedAt = DateTime.UtcNow;

            // LastSeen NÃO é atualizado: ele registra o último sinal de vida, e é o
            // que o varredor usa para decidir quem está velho demais.
            return await _deviceRepository.UpdateAsync(device) ?? device;
        }
    }
}
