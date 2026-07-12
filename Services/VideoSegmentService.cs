using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;

namespace WiseMonitor.Api.Services
{
    public class VideoSegmentService : IVideoSegmentService
    {
        private readonly IVideoSegmentRepository _repository;
        private readonly AppDbContext _context;
        private readonly ILiveMonitoringService _liveService;
        private readonly TimeSpan _retentionWindow;

        public VideoSegmentService(
            IVideoSegmentRepository repository,
            AppDbContext context,
            ILiveMonitoringService liveService,
            IConfiguration configuration)
        {
            _repository = repository;
            _context = context;
            _liveService = liveService;

            var retentionHours = configuration.GetValue<double?>("VideoSegmentRetentionHours") ?? 4;
            _retentionWindow = TimeSpan.FromHours(retentionHours);
        }

        public async Task SaveSegmentAsync(VideoSegmentUploadDTO dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (dto.Segment == null || dto.Segment.Length == 0)
                throw new ArgumentException("Arquivo de vídeo inválido.");

            if (dto.OrganizationId == Guid.Empty)
                throw new ArgumentException("OrganizationId é obrigatório.");

            if (dto.MonitoredUserId == Guid.Empty)
                throw new ArgumentException("MonitoredUserId é obrigatório.");

            if (string.IsNullOrWhiteSpace(dto.DeviceId))
                throw new ArgumentException("DeviceId é obrigatório.");

            byte[] videoBytes;
            using (var ms = new MemoryStream())
            {
                await dto.Segment.CopyToAsync(ms);
                videoBytes = ms.ToArray();
            }

            var segment = new VideoSegment
            {
                OrganizationId = dto.OrganizationId,
                MonitoredUserId = dto.MonitoredUserId,
                DeviceId = dto.DeviceId,
                StartedAt = DateTime.SpecifyKind(dto.StartedAt, DateTimeKind.Utc),
                EndedAt = DateTime.SpecifyKind(dto.EndedAt, DateTimeKind.Utc),
                VideoData = videoBytes,
                ContentType = string.IsNullOrWhiteSpace(dto.Segment.ContentType) ? "video/mp4" : dto.Segment.ContentType,
                SizeInBytes = videoBytes.LongLength,
                CreatedAt = DateTime.UtcNow
            };

            await _repository.UpsertAsync(segment, _retentionWindow);

            await _liveService.NotifyNewSegmentAsync(
                segment.DeviceId,
                segment.OrganizationId.ToString(),
                segment.Id,
                segment.StartedAt,
                segment.EndedAt);
        }

        public Task<VideoSegment?> GetByIdAsync(Guid id) => _repository.GetByIdAsync(id);

        public async Task<VideoSegmentDTO?> GetLatestAsync(string deviceId, string baseUrl)
        {
            var segment = await _repository.GetLatestAsync(deviceId);
            return segment == null ? null : ToDTO(segment, baseUrl);
        }

        public async Task<IEnumerable<VideoSegmentHistoryItemDTO>> GetHistoryWithContextAsync(
            string deviceId, DateTime from, DateTime to, string baseUrl)
        {
            from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
            to = DateTime.SpecifyKind(to, DateTimeKind.Utc);

            var segments = (await _repository.GetHistoryAsync(deviceId, from, to)).ToList();
            if (segments.Count == 0)
                return Enumerable.Empty<VideoSegmentHistoryItemDTO>();

            // Todos os segmentos de um device pertencem ao mesmo MonitoredUserId/OrganizationId
            // na prática — usamos o primeiro para escopar as consultas de contexto.
            var orgId = segments[0].OrganizationId;
            var userId = segments[0].MonitoredUserId;

            // Overlap real (não "mesmo dia"): registro começa antes do fim da janela E
            // (ainda não terminou OU termina depois do início da janela).
            var appFocusEvents = await _context.AppFocusEvents
                .AsNoTracking()
                .Where(a => a.OrganizationId == orgId && a.UserId == userId
                         && a.StartTime < to
                         && (a.EndTime == null || a.EndTime > from))
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            var keyboardSessions = await _context.KeyboardSessions
                .AsNoTracking()
                .Include(k => k.Words)
                .Where(k => k.OrganizationId == orgId && k.UserId == userId
                         && k.StartAt < to && k.EndAt > from)
                .OrderBy(k => k.StartAt)
                .ToListAsync();

            var results = new List<VideoSegmentHistoryItemDTO>(segments.Count);

            foreach (var segment in segments)
            {
                var context = new VideoSegmentContextDTO
                {
                    AppFocusEvents = appFocusEvents
                        .Where(a => a.StartTime < segment.EndedAt && (a.EndTime == null || a.EndTime > segment.StartedAt))
                        .Select(a => new AppFocusContextItemDTO
                        {
                            ApplicationName = a.ApplicationName,
                            WindowTitle = a.WindowTitle,
                            Url = a.Url,
                            StartTime = a.StartTime,
                            EndTime = a.EndTime
                        })
                        .ToList(),

                    KeyboardSessions = keyboardSessions
                        .Where(k => k.StartAt < segment.EndedAt && k.EndAt > segment.StartedAt)
                        .Select(k => new KeyboardContextItemDTO
                        {
                            Application = k.Application,
                            StartAt = k.StartAt,
                            EndAt = k.EndAt,
                            TotalKeystrokes = k.TotalKeystrokes,
                            WordsCount = k.WordsCount,
                            TopWords = k.Words
                                .OrderByDescending(w => w.Count)
                                .Take(10)
                                .Select(w => w.Word ?? string.Empty)
                                .ToList()
                        })
                        .ToList()
                };

                results.Add(new VideoSegmentHistoryItemDTO
                {
                    Segment = ToDTO(segment, baseUrl),
                    Context = context
                });
            }

            return results;
        }

        private static VideoSegmentDTO ToDTO(VideoSegment segment, string baseUrl) => new()
        {
            Id = segment.Id,
            DeviceId = segment.DeviceId,
            MonitoredUserId = segment.MonitoredUserId,
            StartedAt = segment.StartedAt,
            EndedAt = segment.EndedAt,
            Url = $"{baseUrl}/api/video-segments/{segment.Id}"
        };
    }
}
