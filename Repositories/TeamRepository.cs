using Microsoft.EntityFrameworkCore;
using System.Linq;
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

        public async Task<Team?> GetByUserIdAsync(Guid userId, Guid organizationId)
        {
            return await _context.Teams
                .Include(t => t.Members)
                .FirstOrDefaultAsync(t =>
                    t.OrganizationId == organizationId &&
                    t.Members.Any(m => m.UserId == userId));
        }

        public async Task<List<Guid>> GetManagedMemberIdsAsync(Guid managerUserId, Guid organizationId)
        {
            var teams = await _context.Teams
                .Include(t => t.Members)
                .Where(t =>
                    t.OrganizationId == organizationId &&
                    (t.ManagerId == managerUserId ||
                     t.Members.Any(m => m.UserId == managerUserId && m.IsManager)))
                .ToListAsync();

            return teams
                .SelectMany(t => t.Members.Select(m => m.UserId))
                .Distinct()
                .ToList();
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
