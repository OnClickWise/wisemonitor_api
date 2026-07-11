using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User> CreateAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<IEnumerable<User>> GetAllAsync(Guid organizationId)
        {
            return await _context.Users
                .Where(u => u.OrganizationId == organizationId)
                .ToListAsync();
        }

        public async Task<User?> GetByIdAsync(Guid id, Guid organizationId)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.OrganizationId == organizationId);
        }

        public async Task<bool> UpdateAsync(User user)
        {
            _context.Users.Update(user);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(Guid id, Guid organizationId)
        {
            var user = await GetByIdAsync(id, organizationId);
            if (user == null) return false;

            _context.Users.Remove(user);
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
