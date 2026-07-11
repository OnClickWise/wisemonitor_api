using System;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.Models
{
    public class PlatformSettings
    {
        [Key]
        public int Id { get; set; } = 1;

        // Registro
        public bool AllowPublicRegistration { get; set; } = true;
        public bool RequireEmailVerification { get; set; } = true;
        public string DefaultPlanForNewTenants { get; set; } = "Free";
        public int TrialDurationDays { get; set; } = 14;

        // Segurança
        public bool EnforceIpAllowlistForSuperAdmin { get; set; } = false;
        public string AllowedIpsJson { get; set; } = "[]";
        public int SessionTimeoutMinutes { get; set; } = 60;
        public int MaxLoginAttempts { get; set; } = 5;
        public int LockoutDurationMinutes { get; set; } = 30;
        public bool RequireMfaForSuperAdmin { get; set; } = false;

        // Monitoramento
        public int GlobalMaxScreenshotRetentionDays { get; set; } = 90;
        public int ScreenshotCompressionQuality { get; set; } = 80;

        // Modo de manutenção
        public bool MaintenanceModeEnabled { get; set; } = false;
        public string? MaintenanceModeMessage { get; set; }
        public DateTime? MaintenanceScheduledAt { get; set; }

        // Notificações internas
        public bool NotifyOnNewTenantSignup { get; set; } = true;
        public bool NotifyOnPaymentFailure { get; set; } = true;
        public bool NotifyOnCriticalErrors { get; set; } = true;
        public string NotificationEmailsJson { get; set; } = "[]";

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public Guid? LastUpdatedByUserId { get; set; }
    }
}
