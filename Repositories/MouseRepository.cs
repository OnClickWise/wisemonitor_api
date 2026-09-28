using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Helpers;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Data;

namespace WiseMonitor.Api.Repositories
{
    public class MouseRepository : IMouseRepository
    {
        private readonly AppDbContext _context;

        public MouseRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(MouseSession session)
        {
            _context.MouseSessions.Add(session);
            await _context.SaveChangesAsync();
        }

        public async Task<MouseSession> GetByIdAsync(Guid id, Guid userId)
        {
            return await _context.MouseSessions
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        }

        // Sessão que cruza o período também entra (ver KeyboardRepository.InPeriod).
        private IQueryable<MouseSession> InPeriod(DateTime start, DateTime end)
        {
            var (from, to) = ActivityPeriod.Days(start, end);
            from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
            to = DateTime.SpecifyKind(to, DateTimeKind.Utc);

            return _context.MouseSessions
                .AsNoTracking()
                .Where(x => x.StartAt < to && x.EndAt >= from);
        }

        public async Task<IEnumerable<MouseSession>> GetHistoryAsync(
            Guid userId, DateTime start, DateTime end)
        {
            return await InPeriod(start, end)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.StartAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<MouseSession>> GetByOrganizationAsync(
            Guid organizationId, DateTime start, DateTime end, IReadOnlyCollection<Guid>? userIds)
        {
            var query = InPeriod(start, end)
                .Where(x => x.OrganizationId == organizationId);

            if (userIds != null)
                query = query.Where(x => userIds.Contains(x.UserId));

            return await query
                .OrderByDescending(x => x.StartAt)
                .ToListAsync();
        }

        public async Task<MouseSummaryDTO> GetSummaryAsync(
            Guid userId, DateTime start, DateTime end)
        {
            var summary = await InPeriod(start, end)
                .Where(x => x.UserId == userId)
                .GroupBy(_ => 1)
                .Select(g => new MouseSummaryDTO
                {
                    TotalLeftClicks = g.Sum(x => x.LeftClicks),
                    TotalRightClicks = g.Sum(x => x.RightClicks),
                    TotalMiddleClicks = g.Sum(x => x.MiddleClicks),
                    TotalScrollCount = g.Sum(x => x.ScrollCount)
                })
                .FirstOrDefaultAsync();

            // Sem sessões no período devolve zerado em vez de null (que virava 204 sem corpo).
            return summary ?? new MouseSummaryDTO();
        }

        public async Task UpdateAsync(MouseSession session)
        {
            _context.MouseSessions.Update(session);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(MouseSession session)
        {
            _context.MouseSessions.Remove(session);
            await _context.SaveChangesAsync();
        }
    }
}
