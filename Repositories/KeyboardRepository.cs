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
    public class KeyboardRepository : IKeyboardRepository
    {
        private readonly AppDbContext _context;

        public KeyboardRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(KeyboardSession session)
        {
            _context.KeyboardSessions.Add(session);
            await _context.SaveChangesAsync();
        }

        public async Task<KeyboardSession> GetByIdAsync(Guid id, Guid userId)
        {
            return await _context.KeyboardSessions
                .Include(x => x.Words)
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        }

        // Sessão que cruza o período também entra: o agent reaproveita a mesma sessão
        // enquanto está ligado, então ela pode ter começado antes do dia consultado.
        private IQueryable<KeyboardSession> InPeriod(DateTime start, DateTime end)
        {
            var (from, to) = ActivityPeriod.Days(start, end);
            from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
            to = DateTime.SpecifyKind(to, DateTimeKind.Utc);

            return _context.KeyboardSessions
                .AsNoTracking()
                .Where(x => x.StartAt < to && x.EndAt >= from);
        }

        public async Task<IEnumerable<KeyboardSession>> GetHistoryAsync(
            Guid userId, DateTime start, DateTime end)
        {
            return await InPeriod(start, end)
                .Include(x => x.Words)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.StartAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<KeyboardSession>> GetByOrganizationAsync(
            Guid organizationId, DateTime start, DateTime end, IReadOnlyCollection<Guid>? userIds)
        {
            var query = InPeriod(start, end)
                .Include(x => x.Words)
                .Where(x => x.OrganizationId == organizationId);

            if (userIds != null)
                query = query.Where(x => userIds.Contains(x.UserId));

            return await query
                .OrderByDescending(x => x.StartAt)
                .ToListAsync();
        }

        public async Task<KeyboardSummaryDTO> GetSummaryAsync(
            Guid userId, DateTime start, DateTime end)
        {
            var grouped = await InPeriod(start, end)
                .Where(x => x.UserId == userId)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalKeystrokes = g.Sum(x => x.TotalKeystrokes),
                    TotalWords = g.Sum(x => x.WordsCount),
                    AverageScore = g.Average(x => x.ProductivityScore)
                })
                .FirstOrDefaultAsync();

            // Sem sessões no período devolve zerado em vez de null (que virava 204 sem corpo).
            if (grouped == null)
                return new KeyboardSummaryDTO { Classification = KeyboardClassification.Improdutivo };

            var classification =
                grouped.AverageScore >= 70 ? KeyboardClassification.Produtivo :
                grouped.AverageScore >= 40 ? KeyboardClassification.Neutro :
                                              KeyboardClassification.Improdutivo;

            return new KeyboardSummaryDTO
            {
                TotalKeystrokes = grouped.TotalKeystrokes,
                TotalWords = grouped.TotalWords,
                ProductivityScore = (int)grouped.AverageScore,
                Classification = classification
            };
        }

        public async Task UpdateAsync(KeyboardSession session)
        {
            _context.KeyboardSessions.Update(session);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(KeyboardSession session)
        {
            _context.KeyboardSessions.Remove(session);
            await _context.SaveChangesAsync();
        }
    }
}