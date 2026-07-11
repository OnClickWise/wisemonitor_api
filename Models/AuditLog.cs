using System;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.Models
{
    public class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // null = ação do SuperAdmin fora de tenant
        public Guid? OrganizationId { get; set; }

        public Guid? UserId { get; set; }

        [MaxLength(200)]
        public string UserEmail { get; set; } = string.Empty;

        [MaxLength(100)]
        public string UserRole { get; set; } = string.Empty;

        // login | logout | view | export | create | update | delete
        // permission_change | delegation | access_denied
        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = string.Empty;

        // User | Team | Department | Screenshot | etc.
        [MaxLength(100)]
        public string EntityType { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? EntityId { get; set; }

        // JSON serializado do valor anterior
        public string? OldValue { get; set; }

        // JSON serializado do novo valor
        public string? NewValue { get; set; }

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        public bool Success { get; set; } = true;

        [MaxLength(1000)]
        public string? Details { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
