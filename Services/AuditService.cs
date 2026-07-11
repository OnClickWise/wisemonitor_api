using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs.AuditLog;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public class AuditService : IAuditService
    {
        private readonly AppDbContext _context;

        public AuditService(AppDbContext context) => _context = context;

        public async Task LogAsync(
            string action,
            string entityType,
            string? entityId = null,
            string? details = null,
            string? oldValue = null,
            string? newValue = null,
            bool success = true,
            Guid? organizationId = null,
            Guid? userId = null,
            string? userEmail = null,
            string? userRole = null,
            string? ipAddress = null,
            string? userAgent = null)
        {
            var log = new AuditLog
            {
                OrganizationId = organizationId,
                UserId = userId,
                UserEmail = userEmail ?? string.Empty,
                UserRole = userRole ?? string.Empty,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                OldValue = oldValue,
                NewValue = newValue,
                Success = success,
                IpAddress = ipAddress,
                UserAgent = userAgent,
            };

            _context.AuditLogs.Add(log);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Audit não deve quebrar o fluxo principal
                Console.Error.WriteLine($"[AuditService] Falha ao salvar log: {ex.Message}");
            }
        }

        public async Task<AuditLogPagedDTO> GetLogsAsync(AuditLogFilterDTO filter, Guid? organizationId)
        {
            var query = _context.AuditLogs.AsNoTracking().AsQueryable();

            // SuperAdmin vê todos; TenantAdmin filtra por org
            if (organizationId.HasValue)
                query = query.Where(l => l.OrganizationId == organizationId);

            if (!string.IsNullOrWhiteSpace(filter.Action))
                query = query.Where(l => l.Action == filter.Action);

            if (!string.IsNullOrWhiteSpace(filter.EntityType))
                query = query.Where(l => l.EntityType == filter.EntityType);

            if (filter.UserId.HasValue)
                query = query.Where(l => l.UserId == filter.UserId);

            if (filter.From.HasValue)
                query = query.Where(l => l.CreatedAt >= filter.From.Value);

            if (filter.To.HasValue)
                query = query.Where(l => l.CreatedAt <= filter.To.Value);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(l => new AuditLogDTO
                {
                    Id = l.Id,
                    OrganizationId = l.OrganizationId,
                    UserId = l.UserId,
                    UserEmail = l.UserEmail,
                    UserRole = l.UserRole,
                    Action = l.Action,
                    EntityType = l.EntityType,
                    EntityId = l.EntityId,
                    OldValue = l.OldValue,
                    NewValue = l.NewValue,
                    IpAddress = l.IpAddress,
                    Success = l.Success,
                    Details = l.Details,
                    CreatedAt = l.CreatedAt,
                })
                .ToListAsync();

            return new AuditLogPagedDTO
            {
                Items = items,
                TotalCount = total,
                Page = filter.Page,
                PageSize = filter.PageSize,
            };
        }
    }
}
