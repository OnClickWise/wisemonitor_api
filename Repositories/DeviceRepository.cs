using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;

namespace WiseMonitor.Api.Repositories
{
    public class DeviceRepository : IDeviceRepository
    {
        private readonly AppDbContext _context;

        public DeviceRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Device> CreateAsync(Device device)
        {
            await _context.Devices.AddAsync(device);
            await _context.SaveChangesAsync();
            return device;
        }

        public async Task<Device?> GetByIdAsync(Guid id, Guid orgId)
        {
            return await _context.Devices
                .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == orgId);
        }

        public async Task<Device?> GetByHostnameAsync(string hostname, Guid orgId)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                return null;

            // Hostname do Windows é case-insensitive; comparar sem normalizar
            // criaria um device novo só porque o caso mudou.
            return await _context.Devices
                .FirstOrDefaultAsync(d =>
                    d.OrganizationId == orgId &&
                    d.Hostname.ToLower() == hostname.ToLower());
        }

        public async Task<int> MarkStaleOfflineAsync(DateTime threshold, CancellationToken ct = default)
        {
            return await _context.Devices
                .Where(d => d.IsOnline && d.LastSeen < threshold)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(d => d.IsOnline, false)
                        .SetProperty(d => d.UpdatedAt, DateTime.UtcNow),
                    ct);
        }

        public async Task<IEnumerable<Device>> GetAllAsync(Guid orgId)
        {
            return await _context.Devices
                .AsNoTracking()
                .Where(d => d.OrganizationId == orgId)
                .ToListAsync();
        }

        public async Task<Device?> UpdateAsync(Device device)
        {
            var existing = await _context.Devices
                .FirstOrDefaultAsync(d => d.Id == device.Id && d.OrganizationId == device.OrganizationId);

            if (existing == null)
                return null;

            _context.Entry(existing).CurrentValues.SetValues(device);
            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(Guid id, Guid orgId)
        {
            var existing = await _context.Devices
                .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == orgId);

            if (existing == null)
                return false;

            _context.Devices.Remove(existing);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
