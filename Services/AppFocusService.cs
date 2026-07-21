using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Helpers;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Repositories;

namespace WiseMonitor.Api.Services;

/// <summary>
/// Serviço responsável pela lógica de negócio
/// relacionada ao monitoramento de App Focus.
/// </summary>
public class AppFocusService : IAppFocusService
{
    private readonly IAppFocusRepository _repository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IActivityClassificationService _classificationService;
    private readonly ILogger<AppFocusService> _logger;

    public AppFocusService(
        IAppFocusRepository repository,
        IDeviceRepository deviceRepository,
        IActivityClassificationService classificationService,
        ILogger<AppFocusService> logger)
    {
        _repository = repository;
        _deviceRepository = deviceRepository;
        _classificationService = classificationService;
        _logger = logger;
    }

    // ============================================================
    // DELETE
    // ============================================================
    public async Task DeleteAsync(Guid id, Guid organizationId)
    {
        _logger.LogWarning("🗑️ [Service] Removendo AppFocus | EventId={EventId} | Org={OrgId}", id, organizationId);

        var entity = await _repository.GetByIdAsync(id, organizationId);

        if (entity == null)
        {
            _logger.LogError("❌ [Service] Evento AppFocus não encontrado | EventId={EventId}", id);
            throw new Exception("Evento AppFocus não encontrado.");
        }

        await _repository.DeleteAsync(entity.Id);

        _logger.LogInformation("✅ [Service] AppFocus removido com sucesso | EventId={EventId}", id);
    }

    // ============================================================
    // GET ALL 
    // ============================================================
    public async Task<IEnumerable<AppFocusEventResponseDTO>> GetAllAsync(
    Guid organizationId,
    DateTime startDate,
    DateTime endDate)
    {
        _logger.LogInformation(
            "📊 [Service] Buscando AppFocus | Org={OrgId} | Período={Start} até {End}",
            organizationId,
            startDate,
            endDate);

        var events = await _repository.GetByOrganizationAndPeriodAsync(
            organizationId,
            startDate,
            endDate);

        _logger.LogInformation(
            "📈 [Service] {Count} eventos encontrados",
            events.Count());

        return events.Select(e => new AppFocusEventResponseDTO
        {
            Id = e.Id,
            ApplicationName = e.ApplicationName,
            ProcessName = e.ProcessName,
            WindowTitle = e.WindowTitle,
            Url = e.Url,
            FaviconUrl = e.FaviconUrl,
            IconBase64 = e.IconBase64,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            DurationSeconds = e.DurationSeconds,
            Category = e.Category
        });
    }

    // ============================================================
    // GET HISTORY 
    // ============================================================
    public async Task<IEnumerable<AppFocusEvent>> GetHistoryAsync(Guid userId, DateTime start, DateTime end)
    {
        // O Service (GetHistoryAsync) chama o Repository (GetByUserDateRangeAsync)
        return await _repository.GetByUserDateRangeAsync(userId, start, end);
    }

    // ============================================================
    // GET BY ID
    // ============================================================
    public async Task<AppFocusEventResponseDTO?> GetByIdAsync(Guid id, Guid organizationId)
    {
        _logger.LogDebug("🔍 [Service] Buscando AppFocus | EventId={EventId} | Org={OrgId}", id, organizationId);

        var e = await _repository.GetByIdAsync(id, organizationId);

        if (e == null)
        {
            _logger.LogWarning("⚠️ [Service] Evento não encontrado | EventId={EventId}", id);
            return null;
        }

        return new AppFocusEventResponseDTO
        {
            Id = e.Id,
            ApplicationName = e.ApplicationName,
            ProcessName = e.ProcessName,
            WindowTitle = e.WindowTitle,
            Url = e.Url,
            FaviconUrl = e.FaviconUrl,
            IconBase64 = e.IconBase64,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            DurationSeconds = e.DurationSeconds,
            Category = e.Category
        };
    }

    // ============================================================
    // MÉTRICAS
    // ============================================================
    public async Task<IEnumerable<object>> GetMetricsAsync(Guid userId, DateTime date)
    {
        _logger.LogInformation("📊 [Service] Gerando métricas | User={UserId} | Date={Date}", userId, date.Date);

        // 👇 CORREÇÃO: Usamos o método Range, passando a mesma data duas vezes (Início e Fim iguais)
        var events = await _repository.GetByUserDateRangeAsync(userId, date, date);

        return events
            .GroupBy(e => e.Category)
            .Select(g => new
            {
                Category = g.Key,
                TotalSeconds = g.Sum(x => x.DurationSeconds)
            });
    }

    // ============================================================
    // CREATE
    // ============================================================
    public async Task RegisterEventAsync(AppFocusEventCreateDTO dto, Guid userId, Guid organizationId)
    {
        _logger.LogInformation("📥 [Service] AppFocus recebido | Org={OrgId} | User={UserId} | Device={DeviceId} | App={App}", 
            organizationId, userId, dto.DeviceId, dto.ApplicationName);

        var device = await _deviceRepository.GetByIdAsync(dto.DeviceId, organizationId);

        if (device == null)
        {
            _logger.LogWarning("🖥️ [Service] Device não encontrado. Criando | Device={DeviceId}", dto.DeviceId);

            device = new Device
            {
                Id = dto.DeviceId,
                OrganizationId = organizationId,
                Hostname = "Desktop Agent",
                IsOnline = true,
                LastSeen = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _deviceRepository.CreateAsync(device);
        }
        else
        {
            device.IsOnline = true;
            device.LastSeen = DateTime.UtcNow;
            device.UpdatedAt = DateTime.UtcNow;
            await _deviceRepository.UpdateAsync(device);
        }

        var category = await _classificationService.ClassifyAsync(dto.ApplicationName);
        var duration = AppFocusDurationHelper.CalculateDuration(dto.StartTime, dto.EndTime);

        if (duration <= 0)
        {
            _logger.LogWarning("⚠️ [Service] Evento descartado (duração inválida) | App={App}", dto.ApplicationName);
            return;
        }

        var entity = new AppFocusEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            DeviceId = dto.DeviceId,
            ApplicationName = dto.ApplicationName,
            ProcessName = dto.ProcessName,
            WindowTitle = dto.WindowTitle,
            Url = dto.Url,
            FaviconUrl = dto.FaviconUrl,
            IconBase64 = dto.IconBase64,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            DurationSeconds = duration,
            Category = category,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(entity);

        _logger.LogInformation("✅ [Service] AppFocus salvo | EventId={EventId}", entity.Id);
    }

    // ============================================================
    // UPDATE
    // ============================================================
    public async Task UpdateAsync(Guid id, AppFocusEventUpdateDTO dto, Guid organizationId)
    {
        _logger.LogInformation("✏️ [Service] Atualizando AppFocus | EventId={EventId}", id);

        var entity = await _repository.GetByIdAsync(id, organizationId);

        if (entity == null) throw new Exception("Evento AppFocus não encontrado.");

        var duration = AppFocusDurationHelper.CalculateDuration(dto.StartTime, dto.EndTime);
        if (duration <= 0) throw new Exception("Duração inválida.");

        entity.ApplicationName = dto.ApplicationName;
        entity.ProcessName = dto.ProcessName;
        entity.WindowTitle = dto.WindowTitle;
        entity.Url = dto.Url;
        entity.FaviconUrl = dto.FaviconUrl;
        entity.StartTime = dto.StartTime;
        entity.EndTime = dto.EndTime;
        entity.DurationSeconds = duration;
        entity.Category = await _classificationService.ClassifyAsync(dto.ApplicationName);

        await _repository.UpdateAsync(entity);

        _logger.LogInformation("✅ [Service] AppFocus atualizado | EventId={EventId}", id);
    }
} 
// Fim da classe AppFocusService