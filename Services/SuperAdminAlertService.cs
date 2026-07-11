using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Utils;

namespace WiseMonitor.Api.Services
{
    public class SuperAdminAlertService : ISuperAdminAlertService
    {
        private readonly AppDbContext _context;

        public SuperAdminAlertService(AppDbContext context) => _context = context;

        public async Task<PagedResult<AlertRuleResponseDTO>> GetRulesAsync(CancellationToken ct = default)
        {
            var items = await _context.AlertRules.AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => MapToDTO(r))
                .ToListAsync(ct);

            return new PagedResult<AlertRuleResponseDTO>
            {
                Items = items,
                Meta = new PaginationMeta { Page = 1, Limit = items.Count, Total = items.Count }
            };
        }

        public async Task<AlertRuleResponseDTO> CreateRuleAsync(CreateAlertRuleDTO dto, CancellationToken ct = default)
        {
            var rule = new AlertRule
            {
                Name = dto.Name,
                Trigger = dto.Trigger,
                ConditionOperator = dto.ConditionOperator,
                ConditionValue = dto.ConditionValue,
                Severity = dto.Severity,
                NotificationChannelsJson = JsonSerializer.Serialize(dto.NotificationChannels),
                NotificationRecipientsJson = JsonSerializer.Serialize(dto.NotificationRecipients),
                IsActive = dto.IsActive,
            };

            _context.AlertRules.Add(rule);
            await _context.SaveChangesAsync(ct);

            return MapToDTO(rule);
        }

        public async Task UpdateRuleAsync(Guid id, UpdateAlertRuleDTO dto, CancellationToken ct = default)
        {
            var rule = await _context.AlertRules.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Regra de alerta não encontrada.");

            if (dto.Name != null) rule.Name = dto.Name;
            if (dto.Severity != null) rule.Severity = dto.Severity;
            if (dto.NotificationChannels != null)
                rule.NotificationChannelsJson = JsonSerializer.Serialize(dto.NotificationChannels);
            if (dto.NotificationRecipients != null)
                rule.NotificationRecipientsJson = JsonSerializer.Serialize(dto.NotificationRecipients);
            if (dto.IsActive.HasValue) rule.IsActive = dto.IsActive.Value;

            rule.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        public async Task DeleteRuleAsync(Guid id, CancellationToken ct = default)
        {
            var rule = await _context.AlertRules.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Regra de alerta não encontrada.");

            _context.AlertRules.Remove(rule);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<PagedResult<AlertHistoryResponseDTO>> GetHistoryAsync(PaginationDTO pagination, CancellationToken ct = default)
        {
            var query = _context.AlertHistories.AsNoTracking()
                .Include(h => h.AlertRule)
                .OrderByDescending(h => h.CreatedAt);

            var total = await query.CountAsync(ct);

            var items = await query
                .Skip((pagination.Page - 1) * pagination.Limit)
                .Take(pagination.Limit)
                .Select(h => new AlertHistoryResponseDTO
                {
                    Id = h.Id,
                    AlertRuleId = h.AlertRuleId,
                    RuleName = h.AlertRule != null ? h.AlertRule.Name : string.Empty,
                    Message = h.Message,
                    Severity = h.Severity,
                    IsResolved = h.IsResolved,
                    ResolvedAt = h.ResolvedAt,
                    CreatedAt = h.CreatedAt,
                })
                .ToListAsync(ct);

            return new PagedResult<AlertHistoryResponseDTO>
            {
                Items = items,
                Meta = new PaginationMeta { Page = pagination.Page, Limit = pagination.Limit, Total = total }
            };
        }

        public async Task ResolveAsync(Guid id, Guid resolvedByUserId, CancellationToken ct = default)
        {
            var history = await _context.AlertHistories.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Alerta não encontrado.");

            history.IsResolved = true;
            history.ResolvedByUserId = resolvedByUserId;
            history.ResolvedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
        }

        private static AlertRuleResponseDTO MapToDTO(AlertRule r) => new()
        {
            Id = r.Id,
            Name = r.Name,
            Trigger = r.Trigger,
            ConditionOperator = r.ConditionOperator,
            ConditionValue = r.ConditionValue,
            Severity = r.Severity,
            NotificationChannels = JsonSerializer.Deserialize<string[]>(r.NotificationChannelsJson) ?? [],
            NotificationRecipients = JsonSerializer.Deserialize<string[]>(r.NotificationRecipientsJson) ?? [],
            IsActive = r.IsActive,
            CreatedAt = r.CreatedAt,
        };
    }
}
