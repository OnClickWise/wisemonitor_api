using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public class TeamRepository : ITeamRepository
    {
        private readonly AppDbContext _context;

        public TeamRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(Team team)
        {
            _context.Teams.Add(team);
            await _context.SaveChangesAsync();
        }

        public async Task<Team?> GetByIdAsync(Guid id, Guid organizationId)
        {
            return await _context.Teams
                .Include(t => t.Manager)
                .Include(t => t.DefaultWorkSchedule)
                .Include(t => t.Members)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(t => t.Id == id && t.OrganizationId == organizationId);
        }

        public async Task<List<Team>> GetAllAsync(Guid organizationId)
        {
            return await _context.Teams
                .Include(t => t.Manager)
                .Include(t => t.DefaultWorkSchedule)
                .Include(t => t.Members)
                    .ThenInclude(m => m.User)
                .Where(t => t.OrganizationId == organizationId)
                .ToListAsync();
        }

        public async Task UpdateAsync(Team team)
        {
            _context.Teams.Update(team);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Team team)
        {
            _context.Teams.Remove(team);
            await _context.SaveChangesAsync();
        }
    }
}
