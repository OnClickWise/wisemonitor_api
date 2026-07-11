using System;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs.AuditLog;

namespace WiseMonitor.Api.Services
{
    public interface IAuditService
    {
        Task LogAsync(
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
            string? userAgent = null);

        Task<AuditLogPagedDTO> GetLogsAsync(AuditLogFilterDTO filter, Guid? organizationId);
    }
}
