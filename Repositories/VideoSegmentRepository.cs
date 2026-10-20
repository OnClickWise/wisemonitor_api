using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public class VideoSegmentRepository : IVideoSegmentRepository
    {
        private static readonly ConcurrentDictionary<string, DateTime> LastCleanup = new();
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

        private readonly AppDbContext _context;

        public VideoSegmentRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<VideoSegment?> GetByIdAsync(Guid id)
        {
            return await _context.VideoSegments.FindAsync(id);
        }

        // Projeções "sem VideoData" para listagens — evita carregar megabytes de vídeo
        // à toa quando só os metadados importam (histórico, "mais recente").
        private static readonly System.Linq.Expressions.Expression<Func<VideoSegment, VideoSegment>> MetadataOnly =
            v => new VideoSegment
            {
                Id = v.Id,
                OrganizationId = v.OrganizationId,
                MonitoredUserId = v.MonitoredUserId,
                DeviceId = v.DeviceId,
                StartedAt = v.StartedAt,
                EndedAt = v.EndedAt,
                ContentType = v.ContentType,
                SizeInBytes = v.SizeInBytes,
                CreatedAt = v.CreatedAt,
                StorageKey = v.StorageKey,
                VideoData = Array.Empty<byte>()
            };

        public async Task<VideoSegment?> GetMetadataByIdAsync(Guid id)
        {
            return await _context.VideoSegments
                .AsNoTracking()
                .Where(v => v.Id == id)
                .Select(MetadataOnly)
                .FirstOrDefaultAsync();
        }

        public async Task<VideoSegment?> GetLatestAsync(string deviceId)
        {
            return await _context.VideoSegments
                .AsNoTracking()
                .Where(v => v.DeviceId == deviceId)
                .OrderByDescending(v => v.StartedAt)
                .Select(MetadataOnly)
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<VideoSegment>> GetHistoryAsync(string deviceId, DateTime from, DateTime to)
        {
            return await _context.VideoSegments
                .AsNoTracking()
                .Where(v => v.DeviceId == deviceId && v.EndedAt >= from && v.StartedAt <= to)
                .OrderBy(v => v.StartedAt)
                .Select(MetadataOnly)
                .ToListAsync();
        }

        // Com VideoData — só para quem precisa dos bytes (vídeo completo da atividade).
        public async Task<IEnumerable<VideoSegment>> GetHistoryWithDataAsync(string deviceId, DateTime from, DateTime to)
        {
            return await _context.VideoSegments
                .AsNoTracking()
                .Where(v => v.DeviceId == deviceId && v.EndedAt >= from && v.StartedAt <= to)
                .OrderBy(v => v.StartedAt)
                .ToListAsync();
        }

        public async Task UpsertAsync(VideoSegment segment, TimeSpan retentionWindow)
        {
            await _context.VideoSegments.AddAsync(segment);
            await _context.SaveChangesAsync();

            // Retenção "de carona" no upload, no máximo uma vez por hora por máquina (ou
            // quando a janela é menor que isso): rodar a cada segmento de 10s eram ~8 DELETEs
            // por segundo no banco. Os arquivos no bucket somem pela regra de ciclo de vida.
            var cleanupKey = $"{segment.OrganizationId}|{segment.DeviceId}";
            var now = DateTime.UtcNow;
            var interval = retentionWindow < CleanupInterval ? TimeSpan.Zero : CleanupInterval;
            if (LastCleanup.TryGetValue(cleanupKey, out var last) && now - last < interval)
                return;
            // ExecuteDelete só existe em banco relacional (alguns testes usam o InMemory).
            if (!_context.Database.IsRelational())
                return;
            LastCleanup[cleanupKey] = now;

            var cutoff = now - retentionWindow;
            // Direto no banco: carregar as entidades trazia o MP4 de cada segmento só
            // para apagá-lo, a cada upload (~10s por máquina).
            await _context.VideoSegments
                .Where(v => v.OrganizationId == segment.OrganizationId
                         && v.DeviceId == segment.DeviceId
                         && v.EndedAt < cutoff)
                .ExecuteDeleteAsync();
        }
    }
}
