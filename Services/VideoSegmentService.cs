using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
        private readonly byte[] _signingKey;

        /// <summary>Quanto tempo uma URL de vídeo assinada continua válida depois de gerada.</summary>
        private static readonly TimeSpan UrlValidity = TimeSpan.FromHours(6);

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

            var secretKey = configuration["Jwt:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
                throw new InvalidOperationException("JWT SecretKey não configurada.");

            _signingKey = Encoding.UTF8.GetBytes(secretKey);
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

        public async Task<VideoSegment?> GetActivityVideoAsync(string deviceId, DateTime from, DateTime to)
        {
            from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
            to = DateTime.SpecifyKind(to, DateTimeKind.Utc);

            var segments = (await _repository.GetHistoryAsync(deviceId, from, to))
                .Where(segment => segment.EndedAt >= from && segment.StartedAt <= to)
                .OrderBy(segment => segment.StartedAt)
                .ToList();

            if (segments.Count == 0)
                return null;

            var temporaryDirectory = Path.Combine(
                Path.GetTempPath(),
                $"wisemonitor_activity_{Guid.NewGuid():N}");

            Directory.CreateDirectory(temporaryDirectory);

            try
            {
                var segmentPaths = new List<string>();

                for (var index = 0; index < segments.Count; index++)
                {
                    var segmentPath = Path.Combine(temporaryDirectory, $"segment_{index:D5}.mp4");
                    await File.WriteAllBytesAsync(segmentPath, segments[index].VideoData);
                    segmentPaths.Add(segmentPath);
                }

                var concatFilePath = Path.Combine(temporaryDirectory, "segments.txt");

                var concatLines = segmentPaths.Select(path =>
                {
                    var escapedPath = path.Replace("\\", "/").Replace("'", "'\\''");
                    return $"file '{escapedPath}'";
                });

                await File.WriteAllLinesAsync(concatFilePath, concatLines);

                var outputPath = Path.Combine(temporaryDirectory, "activity-video.mp4");

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                processStartInfo.ArgumentList.Add("-y");
                processStartInfo.ArgumentList.Add("-f");
                processStartInfo.ArgumentList.Add("concat");
                processStartInfo.ArgumentList.Add("-safe");
                processStartInfo.ArgumentList.Add("0");
                processStartInfo.ArgumentList.Add("-i");
                processStartInfo.ArgumentList.Add(concatFilePath);
                processStartInfo.ArgumentList.Add("-c");
                processStartInfo.ArgumentList.Add("copy");
                processStartInfo.ArgumentList.Add("-movflags");
                processStartInfo.ArgumentList.Add("+faststart");
                processStartInfo.ArgumentList.Add(outputPath);

                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                var errorOutputTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                var errorOutput = await errorOutputTask;

                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"Erro ao unir os vídeos com FFmpeg: {errorOutput}");

                if (!File.Exists(outputPath))
                    throw new FileNotFoundException("O FFmpeg não gerou o vídeo completo.");

                var videoData = await File.ReadAllBytesAsync(outputPath);

                var firstSegment = segments.First();
                var lastSegment = segments.Last();

                return new VideoSegment
                {
                    OrganizationId = firstSegment.OrganizationId,
                    MonitoredUserId = firstSegment.MonitoredUserId,
                    DeviceId = deviceId,
                    StartedAt = firstSegment.StartedAt,
                    EndedAt = lastSegment.EndedAt,
                    ContentType = "video/mp4",
                    VideoData = videoData,
                    SizeInBytes = videoData.LongLength
                };
            }
            finally
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    try
                    {
                        Directory.Delete(temporaryDirectory, recursive: true);
                    }
                    catch
                    {
                        // A limpeza dos arquivos temporários não deve impedir o retorno do vídeo.
                    }
                }
            }
        }

        private VideoSegmentDTO ToDTO(VideoSegment segment, string baseUrl) => new()
        {
            Id = segment.Id,
            DeviceId = segment.DeviceId,
            MonitoredUserId = segment.MonitoredUserId,
            StartedAt = segment.StartedAt,
            EndedAt = segment.EndedAt,
            Url = BuildSignedUrl(baseUrl, segment.Id)
        };

        /// <summary>
        /// GetById (quem serve o binário do vídeo) precisa ser anônimo — a tag &lt;video&gt;
        /// do navegador não manda header de autenticação. Sem alguma barreira, qualquer
        /// pessoa, mesmo sem login, sabendo ou adivinhando um GUID, acessaria o vídeo de
        /// outro tenant (o filtro de tenant do EF é ignorado em requisições anônimas). A URL
        /// carrega uma assinatura HMAC com prazo de validade, gerada aqui (onde já sabemos
        /// que quem pediu o histórico tinha acesso legítimo a esse segmento) e conferida em
        /// GetById antes de servir o arquivo.
        /// </summary>
        private string BuildSignedUrl(string baseUrl, Guid id)
        {
            var exp = DateTimeOffset.UtcNow.Add(UrlValidity).ToUnixTimeSeconds();
            var sig = Sign(id, exp);
            return $"{baseUrl}/api/video-segments/{id}?exp={exp}&sig={sig}";
        }

        public bool ValidateAccess(Guid id, long exp, string? sig)
        {
            if (string.IsNullOrWhiteSpace(sig))
                return false;

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
                return false;

            var expected = Sign(id, exp);

            // Comparação em tempo constante: comparar assinatura com == vazaria, por
            // timing, quantos caracteres batem — dá pra forjar a assinatura aos poucos.
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(sig));
        }

        private string Sign(Guid id, long exp)
        {
            var payload = $"{id}|{exp}";
            var hash = HMACSHA256.HashData(_signingKey, Encoding.UTF8.GetBytes(payload));
            return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
    }
}
