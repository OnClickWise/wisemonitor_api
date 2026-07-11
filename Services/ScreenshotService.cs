using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;

namespace WiseMonitor.Api.Services
{
    public class ScreenshotService : IScreenshotService
    {
        private readonly IScreenshotRepository _repository;

        public ScreenshotService(IScreenshotRepository repository)
        {
            _repository = repository;
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
                ScreenshotUrl = $"/api/screenshots/{screenshot.Id}",
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
    }
}
