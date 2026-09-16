using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public class ApplicationDisplayNameRepository
        : IApplicationDisplayNameRepository
    {
        private readonly AppDbContext _context;

        public ApplicationDisplayNameRepository(
            AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ApplicationDisplayName>>
            GetByOrganizationAsync(Guid organizationId)
        {
            return await _context.ApplicationDisplayNames
                .Where(a => a.OrganizationId == organizationId)
                .AsNoTracking()
                .OrderBy(a => a.DisplayName)
                .ToListAsync();
        }

        public async Task<ApplicationDisplayName?>
            GetByApplicationNameAsync(
                Guid organizationId,
                string applicationName)
        {
            return await _context.ApplicationDisplayNames
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.OrganizationId == organizationId &&
                    a.ApplicationName.ToLower() == applicationName.ToLower());
        }

        public async Task<ApplicationDisplayName> SaveAsync(
            Guid organizationId,
            string applicationName,
            string displayName)
        {
            var normalizedApplicationName = applicationName.Trim();
            var normalizedDisplayName = displayName.Trim();

            var existing = await _context.ApplicationDisplayNames
                .FirstOrDefaultAsync(a =>
                    a.OrganizationId == organizationId &&
                    a.ApplicationName.ToLower() ==
                        normalizedApplicationName.ToLower());

            if (existing != null)
            {
                existing.DisplayName = normalizedDisplayName;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return existing;
            }

            var entity = new ApplicationDisplayName
            {
                OrganizationId = organizationId,
                ApplicationName = normalizedApplicationName,
                DisplayName = normalizedDisplayName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.ApplicationDisplayNames.AddAsync(entity);
            await _context.SaveChangesAsync();

            return entity;
        }
    }
}
