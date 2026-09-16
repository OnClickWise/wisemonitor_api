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

        public async Task<IEnumerable<KeyboardSession>> GetHistoryAsync(
            Guid userId, DateTime start, DateTime end)
        {
            start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

            return await _context.KeyboardSessions
                .Include(x => x.Words)
                .Where(x =>
                    x.UserId == userId &&
                    x.StartAt >= start &&
                    x.EndAt <= end)
                .OrderByDescending(x => x.StartAt)
                .ToListAsync();
        }

        public async Task<KeyboardSummaryDTO> GetSummaryAsync(
            Guid userId, DateTime start, DateTime end)
        {
            start = DateTime.SpecifyKind(start, DateTimeKind.Utc);
            end = DateTime.SpecifyKind(end, DateTimeKind.Utc);

            var grouped = await _context.KeyboardSessions
                .Where(x =>
                    x.UserId == userId &&
                    x.StartAt >= start &&
                    x.EndAt <= end)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    TotalKeystrokes = g.Sum(x => x.TotalKeystrokes),
                    TotalWords = g.Sum(x => x.WordsCount),
                    AverageScore = g.Average(x => x.ProductivityScore)
                })
                .FirstOrDefaultAsync();

            if (grouped == null)
                return null;

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