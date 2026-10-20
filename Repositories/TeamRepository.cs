using Microsoft.EntityFrameworkCore;
using System.Linq;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Models.Enums;

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
            var memberIds = await _context.Teams
                .AsNoTracking()
                .Where(t =>
                    t.OrganizationId == organizationId &&
                    (t.ManagerId == managerUserId ||
                     t.Members.Any(m => m.UserId == managerUserId && m.IsManager)))
                .SelectMany(t => t.Members.Select(m => m.UserId))
                .Distinct()
                .ToListAsync();

            // Mesma regra do AccessScopeService: só quem está abaixo na hierarquia, e ele mesmo.
            var users = await _context.Users
                .AsNoTracking()
                .Where(u => u.OrganizationId == organizationId &&
                            (u.Id == managerUserId || memberIds.Contains(u.Id)))
                .Select(u => new { u.Id, u.Role })
                .ToListAsync();

            var myLevel = UserRoles.GetHierarchyLevel(users.FirstOrDefault(u => u.Id == managerUserId)?.Role);

            return users
                .Where(u => u.Id == managerUserId || UserRoles.GetHierarchyLevel(u.Role) > myLevel)
                .Select(u => u.Id)
                .ToList();
        }

        public async Task SetManagedTeamsAsync(
            Guid userId,
            IReadOnlyCollection<Guid> teamIds,
            Guid organizationId)
        {
            var wanted = teamIds.ToHashSet();

            var teams = await _context.Teams
                .Where(t => t.OrganizationId == organizationId)
                .Select(t => new { t.Id, t.ManagerId })
                .ToListAsync();

            var orgTeamIds = teams.Select(t => t.Id).ToList();
            var rows = await _context.TeamMembers
                .Where(m => m.UserId == userId && orgTeamIds.Contains(m.TeamId))
                .ToListAsync();

            foreach (var team in teams)
            {
                var row = rows.FirstOrDefault(m => m.TeamId == team.Id);

                if (wanted.Contains(team.Id))
                {
                    if (row == null)
                        _context.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = userId, IsManager = true });
                    else
                        row.IsManager = true;
                }
                else if (row is { IsManager: true } && team.ManagerId != userId)
                {
                    // Deixou de gerir: sai da equipe (a atribuição o tinha incluído nela).
                    _context.TeamMembers.Remove(row);
                }
            }

            await _context.SaveChangesAsync();
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
