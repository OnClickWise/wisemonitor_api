using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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
