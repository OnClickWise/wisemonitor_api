using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Repositories
{
    public class VideoSegmentRepository : IVideoSegmentRepository
    {
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
                VideoData = Array.Empty<byte>()
            };

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

        public async Task UpsertAsync(VideoSegment segment, TimeSpan retentionWindow)
        {
            await _context.VideoSegments.AddAsync(segment);
            await _context.SaveChangesAsync();

            var cutoff = DateTime.UtcNow - retentionWindow;
            var old = await _context.VideoSegments
                .Where(v => v.OrganizationId == segment.OrganizationId
                         && v.DeviceId == segment.DeviceId
                         && v.EndedAt < cutoff)
                .ToListAsync();

            if (old.Count > 0)
            {
                _context.VideoSegments.RemoveRange(old);
                await _context.SaveChangesAsync();
            }
        }
    }
}
