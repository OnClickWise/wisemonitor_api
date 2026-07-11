using System;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.DTOs.SuperAdmin
{
    public class AlertRuleResponseDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Trigger { get; set; } = string.Empty;
        public string? ConditionOperator { get; set; }
        public string? ConditionValue { get; set; }
        public string Severity { get; set; } = string.Empty;
        public string[] NotificationChannels { get; set; } = [];
        public string[] NotificationRecipients { get; set; } = [];
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateAlertRuleDTO
    {
        [Required][MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // TenantSignup | TenantSuspended | PaymentFailed | ErrorRateHigh | ResponseTimeHigh
        // StorageLimitNear | SuspiciousLogin | BulkUserDelete | ImpersonationUsed | AgentVersionMismatch
        [Required][MaxLength(50)]
        public string Trigger { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? ConditionOperator { get; set; }

        [MaxLength(200)]
        public string? ConditionValue { get; set; }

        [Required][MaxLength(20)]
        public string Severity { get; set; } = "Info";

        public string[] NotificationChannels { get; set; } = [];
        public string[] NotificationRecipients { get; set; } = [];

        public bool IsActive { get; set; } = true;
    }

    public class UpdateAlertRuleDTO
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(20)]
        public string? Severity { get; set; }

        public string[]? NotificationChannels { get; set; }
        public string[]? NotificationRecipients { get; set; }

        public bool? IsActive { get; set; }
    }

    public class AlertHistoryResponseDTO
    {
        public Guid Id { get; set; }
        public Guid AlertRuleId { get; set; }
        public string RuleName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
