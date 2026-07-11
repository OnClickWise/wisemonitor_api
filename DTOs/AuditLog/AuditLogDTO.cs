using System;

namespace WiseMonitor.Api.DTOs.AuditLog
{
    public class AuditLogDTO
    {
        public Guid Id { get; set; }
        public Guid? OrganizationId { get; set; }
        public Guid? UserId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string UserRole { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string? IpAddress { get; set; }
        public bool Success { get; set; }
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AuditLogFilterDTO
    {
        public string? Action { get; set; }
        public string? EntityType { get; set; }
        public Guid? UserId { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class AuditLogPagedDTO
    {
        public System.Collections.Generic.List<AuditLogDTO> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
