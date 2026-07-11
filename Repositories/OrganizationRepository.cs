using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public class OrganizationRepository: IOrganizationRepository
    {
        private readonly AppDbContext _context;

        public OrganizationRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Organization?> GetByIdWithAdminAsync(Guid id)
        {
            // Esta é a implementação que faltava
            return await _context.Organizations
                .Include(o => o.Users) // Inclui a lista de usuários
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<Organization?> GetOrganizationWithAdminAsync(Guid organizationId)
        {
            return await _context.Organizations
                .Include(o => o.AdminUser)
                .FirstOrDefaultAsync(o => o.Id == organizationId);
        }

        public async Task<Organization?> GetByIdAsync(Guid id)
        {
            return await _context.Organizations
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task UpdateAsync(Organization organization)
        {
            _context.Organizations.Update(organization);
            await _context.SaveChangesAsync();
        }
    }
}

