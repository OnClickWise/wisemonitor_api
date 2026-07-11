using System;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.Models
{
    public class PlatformIntegration
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required][MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // SmtpEmail | Slack | Webhook | Zapier | AwsS3 | Sentry | Datadog | PagerDuty
        [Required][MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        // JSON com as configurações (nunca expor senhas em texto plano)
        public string ConfigJson { get; set; } = "{}";

        public bool IsActive { get; set; } = true;

        // JSON: ["tenant.created","tenant.suspended","billing.failed","error.critical"]
        public string EventsJson { get; set; } = "[]";

        public DateTime? LastTestedAt { get; set; }
        public bool? LastTestSuccess { get; set; }
        public string? LastTestMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
