using System;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.Models
{
    public class AlertRule
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required][MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // TenantSignup | TenantSuspended | PaymentFailed | ErrorRateHigh | ResponseTimeHigh
        // StorageLimitNear | SuspiciousLogin | BulkUserDelete | ImpersonationUsed | AgentVersionMismatch
        [Required][MaxLength(50)]
        public string Trigger { get; set; } = string.Empty;

        // Operador: gt | lt | eq | contains
        [MaxLength(20)]
        public string? ConditionOperator { get; set; }

        [MaxLength(200)]
        public string? ConditionValue { get; set; }

        // Info | Warning | Critical
        [Required][MaxLength(20)]
        public string Severity { get; set; } = "Info";

        // JSON: ["email","slack","webhook"]
        public string NotificationChannelsJson { get; set; } = "[]";

        // JSON: ["admin@example.com"]
        public string NotificationRecipientsJson { get; set; } = "[]";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AlertHistory
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid AlertRuleId { get; set; }
        public AlertRule? AlertRule { get; set; }

        [Required][MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Severity { get; set; } = "Info";

        public bool IsResolved { get; set; } = false;
        public Guid? ResolvedByUserId { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
