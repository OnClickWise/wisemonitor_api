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
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        }

        public async Task<IEnumerable<KeyboardSession>> GetHistoryAsync(
            Guid userId, DateTime start, DateTime end)
        {
            return await _context.KeyboardSessions
                .Where(x =>
                    x.UserId == userId &&
                    x.StartAt >= start &&
                    x.EndAt <= end)
                .ToListAsync();
        }

        public async Task<KeyboardSummaryDTO> GetSummaryAsync(
            Guid userId, DateTime start, DateTime end)
        {
            return await _context.KeyboardSessions
                .Where(x =>
                    x.UserId == userId &&
                    x.StartAt >= start &&
                    x.EndAt <= end)
                .GroupBy(_ => 1)
                .Select(g => new KeyboardSummaryDTO
                {
                    TotalKeystrokes = g.Sum(x => x.TotalKeystrokes),
                    TotalWords = g.Sum(x => x.WordsCount),
                    ProductivityScore = (int)g.Average(x => x.ProductivityScore),
                    Classification = KeyboardClassification.Produtivo
                })
                .FirstOrDefaultAsync();
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