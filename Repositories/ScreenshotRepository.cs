using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public class ScreenshotRepository : IScreenshotRepository
    {
        private readonly AppDbContext _context;

        public ScreenshotRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Screenshot?> GetByIdAsync(Guid id)
        {
            return await _context.Screenshots
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<Screenshot?> GetLastByUserAsync(Guid monitoredUserId)
        {
            return await _context.Screenshots
                .AsNoTracking()
                .Where(s => s.MonitoredUserId == monitoredUserId)
                .OrderByDescending(s => s.CapturedAt)
                .FirstOrDefaultAsync();
        }

        // Os deletes abaixo vão direto no banco (ExecuteDelete): carregar as entidades
        // pra RemoveRange traria o ImageData de cada uma só pra apagá-lo.
        public async Task DeletePreviousAsync(Guid monitoredUserId)
        {
            await _context.Screenshots
                .Where(s => s.MonitoredUserId == monitoredUserId)
                .ExecuteDeleteAsync();
        }

        public async Task UpsertAsync(Screenshot screenshot)
        {
            // Insere a nova screenshot
            await _context.Screenshots.AddAsync(screenshot);
            await _context.SaveChangesAsync();

            // Mantém apenas as últimas 10 por dispositivo para evitar crescimento ilimitado do BD
            // (não apaga tudo antes — evita race condition onde a URL ainda está sendo servida)
            var oldIds = await _context.Screenshots
                .Where(s => s.MonitoredUserId == screenshot.MonitoredUserId
                         && s.DeviceId == screenshot.DeviceId)
                .OrderByDescending(s => s.CapturedAt)
                .Skip(10)
                .Select(s => s.Id)
                .ToListAsync();

            if (oldIds.Count > 0)
            {
                await _context.Screenshots
                    .Where(s => oldIds.Contains(s.Id))
                    .ExecuteDeleteAsync();
            }
        }

        // 🔴 LEGADO — NÃO USAR EM PRODUÇÃO
        public async Task<IEnumerable<Screenshot>> GetAllAsync()
        {
            return await _context.Screenshots
                .AsNoTracking()
                .OrderByDescending(x => x.CapturedAt)
                .ToListAsync();
        }

        // ✅ MULTI-TENANT (CORRETO) — retorna apenas a mais recente por dispositivo
        public async Task<IEnumerable<Screenshot>> GetAllByOrganizationAsync(Guid organizationId)
        {
            // Projeta só os metadados: essa lista é chamada a cada poucos segundos pelas
            // telas de dispositivos, e trazer o ImageData de todas as capturas da org
            // (até 10 por device) pra usar uma por device era o maior custo do backend.
            var all = await _context.Screenshots
                .AsNoTracking()
                .Where(x => x.OrganizationId == organizationId)
                .OrderByDescending(x => x.CapturedAt)
                .Select(x => new Screenshot
                {
                    Id = x.Id,
                    OrganizationId = x.OrganizationId,
                    MonitoredUserId = x.MonitoredUserId,
                    DeviceId = x.DeviceId,
                    CapturedAt = x.CapturedAt
                })
                .ToListAsync();

            // Filtra em memória: 1 screenshot por deviceId (lista já está desc, logo a 1ª é a mais recente)
            var seen = new HashSet<string?>();
            return all.Where(s => seen.Add(s.DeviceId));
        }
    }
}
