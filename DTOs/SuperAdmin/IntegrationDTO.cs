using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.DTOs.SuperAdmin
{
    public class IntegrationResponseDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string[] Events { get; set; } = [];
        public DateTime? LastTestedAt { get; set; }
        public bool? LastTestSuccess { get; set; }
        public string? LastTestMessage { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateIntegrationDTO
    {
        // SmtpEmail | Slack | Webhook | Zapier | AwsS3 | Sentry | Datadog | PagerDuty
        [Required][MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        [Required][MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public Dictionary<string, string> Config { get; set; } = new();

        public bool IsActive { get; set; } = true;

        public string[] Events { get; set; } = [];
    }

    public class UpdateIntegrationDTO
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        public Dictionary<string, string>? Config { get; set; }

        public bool? IsActive { get; set; }

        public string[]? Events { get; set; }
    }

    public class IntegrationTestResultDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime TestedAt { get; set; } = DateTime.UtcNow;
    }
}
