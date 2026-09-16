using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.DTOs;
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

        public async Task<IEnumerable<MouseSession>> GetHistoryAsync(
            Guid userId, DateTime start, DateTime end)
        {
            start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

            return await _context.MouseSessions
                .Where(x =>
                    x.UserId == userId &&
                    x.StartAt >= start &&
                    x.EndAt <= end)
                .OrderByDescending(x => x.StartAt)
                .ToListAsync();
        }

        public async Task<MouseSummaryDTO> GetSummaryAsync(
            Guid userId, DateTime start, DateTime end)
        {
            start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

            return await _context.MouseSessions
                .Where(x =>
                    x.UserId == userId &&
                    x.StartAt >= start &&
                    x.EndAt <= end)
                .GroupBy(_ => 1)
                .Select(g => new MouseSummaryDTO
                {
                    TotalLeftClicks = g.Sum(x => x.LeftClicks),
                    TotalRightClicks = g.Sum(x => x.RightClicks),
                    TotalMiddleClicks = g.Sum(x => x.MiddleClicks),
                    TotalScrollCount = g.Sum(x => x.ScrollCount)
                })
                .FirstOrDefaultAsync();
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
