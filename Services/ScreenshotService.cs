using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;

namespace WiseMonitor.Api.Services
{
    public class ScreenshotService : IScreenshotService
    {
        private readonly IScreenshotRepository _repository;
        private readonly byte[] _signingKey;

        /// <summary>Quanto tempo uma URL de captura assinada continua válida depois de gerada.</summary>
        private static readonly TimeSpan UrlValidity = TimeSpan.FromHours(6);

        public ScreenshotService(IScreenshotRepository repository, IConfiguration configuration)
        {
            _repository = repository;

            var secretKey = configuration["Jwt:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
                throw new InvalidOperationException("JWT SecretKey não configurada.");

            _signingKey = Encoding.UTF8.GetBytes(secretKey);
        }

        public async Task<LiveMonitoringScreenshotResponseDTO> SaveScreenshotAsync(
            LiveMonitoringScreenshotUploadDTO dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (dto.MonitoredUserId == Guid.Empty)
                throw new ArgumentException("MonitoredUserId é obrigatório.");

            if (dto.OrganizationId == Guid.Empty)
                throw new ArgumentException("OrganizationId é obrigatório.");

            if (dto.Screenshot == null || dto.Screenshot.Length == 0)
                throw new ArgumentException("Arquivo inválido.");

            byte[] imageBytes;
            using (var ms = new MemoryStream())
            {
                await dto.Screenshot.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            var screenshot = new Screenshot
            {
                OrganizationId = dto.OrganizationId,
                MonitoredUserId = dto.MonitoredUserId,
                DeviceId = dto.DeviceId,
                FileName = $"{dto.DeviceId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png",
                ImageData = imageBytes,
                ContentType = dto.Screenshot.ContentType ?? "image/png",
                SizeInBytes = imageBytes.LongLength,
                CapturedAt = DateTime.UtcNow
            };

            await _repository.UpsertAsync(screenshot);

            return new LiveMonitoringScreenshotResponseDTO
            {
                Success = true,
                UserId = dto.MonitoredUserId,
                Timestamp = screenshot.CapturedAt,
                ScreenshotUrl = BuildSignedPath(screenshot.Id),
                ThumbnailUrl = string.Empty
            };
        }

        public async Task<LastScreenshotDTO?> GetLastScreenshotByUserAsync(Guid monitoredUserId)
        {
            var entity = await _repository.GetLastByUserAsync(monitoredUserId);
            if (entity == null)
                return null;

            return new LastScreenshotDTO
            {
                UserId = entity.MonitoredUserId,
                ContentType = entity.ContentType,
                Base64Image = Convert.ToBase64String(entity.ImageData),
                CapturedAt = entity.CapturedAt
            };
        }

        // 🔴 LEGADO
        public Task<IEnumerable<Screenshot>> GetAllAsync() =>
            _repository.GetAllAsync();

        // ✅ MULTI-TENANT
        public Task<IEnumerable<Screenshot>> GetAllByOrganizationAsync(Guid organizationId) =>
            _repository.GetAllByOrganizationAsync(organizationId);

        public Task<Screenshot?> GetByIdAsync(Guid id) =>
            _repository.GetByIdAsync(id);

        /// <summary>
        /// Mesmo esquema das URLs de vídeo: o GET da imagem precisa ser anônimo (a tag
        /// &lt;img&gt; não manda header de autenticação) e o filtro de tenant do EF não se
        /// aplica a requisição anônima — sem assinatura, qualquer pessoa com o GUID via a
        /// tela de qualquer empresa. "screenshot|" separa do payload assinado dos vídeos.
        /// </summary>
        public string BuildSignedPath(Guid id)
        {
            var exp = DateTimeOffset.UtcNow.Add(UrlValidity).ToUnixTimeSeconds();
            return $"/api/screenshots/{id}?exp={exp}&sig={Sign(id, exp)}";
        }

        public bool ValidateAccess(Guid id, long exp, string? sig)
        {
            if (string.IsNullOrWhiteSpace(sig))
                return false;

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
                return false;

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(Sign(id, exp)),
                Encoding.UTF8.GetBytes(sig));
        }

        private string Sign(Guid id, long exp)
        {
            var hash = HMACSHA256.HashData(_signingKey, Encoding.UTF8.GetBytes($"screenshot|{id}|{exp}"));
            return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }
    }
}
