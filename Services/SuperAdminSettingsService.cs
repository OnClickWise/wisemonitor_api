using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public class SuperAdminSettingsService : ISuperAdminSettingsService
    {
        private readonly AppDbContext _context;

        public SuperAdminSettingsService(AppDbContext context) => _context = context;

        public async Task<PlatformSettingsDTO> GetAsync(CancellationToken ct = default)
        {
            var settings = await _context.PlatformSettings.FirstOrDefaultAsync(ct);

            if (settings == null)
                return new PlatformSettingsDTO();

            return new PlatformSettingsDTO
            {
                AllowPublicRegistration        = settings.AllowPublicRegistration,
                RequireEmailVerification       = settings.RequireEmailVerification,
                DefaultPlanForNewTenants       = settings.DefaultPlanForNewTenants,
                TrialDurationDays              = settings.TrialDurationDays,
                EnforceIpAllowlistForSuperAdmin = settings.EnforceIpAllowlistForSuperAdmin,
                AllowedIps                     = JsonSerializer.Deserialize<string[]>(settings.AllowedIpsJson) ?? [],
                SessionTimeoutMinutes          = settings.SessionTimeoutMinutes,
                MaxLoginAttempts               = settings.MaxLoginAttempts,
                LockoutDurationMinutes         = settings.LockoutDurationMinutes,
                RequireMfaForSuperAdmin        = settings.RequireMfaForSuperAdmin,
                GlobalMaxScreenshotRetentionDays = settings.GlobalMaxScreenshotRetentionDays,
                ScreenshotCompressionQuality   = settings.ScreenshotCompressionQuality,
                MaintenanceModeEnabled         = settings.MaintenanceModeEnabled,
                MaintenanceModeMessage         = settings.MaintenanceModeMessage,
                MaintenanceScheduledAt         = settings.MaintenanceScheduledAt,
                NotifyOnNewTenantSignup        = settings.NotifyOnNewTenantSignup,
                NotifyOnPaymentFailure         = settings.NotifyOnPaymentFailure,
                NotifyOnCriticalErrors         = settings.NotifyOnCriticalErrors,
                NotificationEmails             = JsonSerializer.Deserialize<string[]>(settings.NotificationEmailsJson) ?? [],
            };
        }

        public async Task UpdateAsync(PlatformSettingsDTO dto, Guid adminId, CancellationToken ct = default)
        {
            var settings = await _context.PlatformSettings.FirstOrDefaultAsync(ct);

            if (settings == null)
            {
                settings = new PlatformSettings { Id = 1 };
                _context.PlatformSettings.Add(settings);
            }

            settings.AllowPublicRegistration         = dto.AllowPublicRegistration;
            settings.RequireEmailVerification        = dto.RequireEmailVerification;
            settings.DefaultPlanForNewTenants        = dto.DefaultPlanForNewTenants;
            settings.TrialDurationDays               = dto.TrialDurationDays;
            settings.EnforceIpAllowlistForSuperAdmin = dto.EnforceIpAllowlistForSuperAdmin;
            settings.AllowedIpsJson                  = JsonSerializer.Serialize(dto.AllowedIps);
            settings.SessionTimeoutMinutes           = dto.SessionTimeoutMinutes;
            settings.MaxLoginAttempts                = dto.MaxLoginAttempts;
            settings.LockoutDurationMinutes          = dto.LockoutDurationMinutes;
            settings.RequireMfaForSuperAdmin         = dto.RequireMfaForSuperAdmin;
            settings.GlobalMaxScreenshotRetentionDays = dto.GlobalMaxScreenshotRetentionDays;
            settings.ScreenshotCompressionQuality    = dto.ScreenshotCompressionQuality;
            settings.MaintenanceModeEnabled          = dto.MaintenanceModeEnabled;
            settings.MaintenanceModeMessage          = dto.MaintenanceModeMessage;
            settings.MaintenanceScheduledAt          = dto.MaintenanceScheduledAt;
            settings.NotifyOnNewTenantSignup         = dto.NotifyOnNewTenantSignup;
            settings.NotifyOnPaymentFailure          = dto.NotifyOnPaymentFailure;
            settings.NotifyOnCriticalErrors          = dto.NotifyOnCriticalErrors;
            settings.NotificationEmailsJson          = JsonSerializer.Serialize(dto.NotificationEmails);
            settings.UpdatedAt                       = DateTime.UtcNow;
            settings.LastUpdatedByUserId             = adminId;

            await _context.SaveChangesAsync(ct);
        }
    }
}
