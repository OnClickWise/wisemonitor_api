using System;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.DTOs.SuperAdmin
{
    // ── Response ─────────────────────────────────────────────────────────────
    public class TenantDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Plan { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int UserCount { get; set; }
        public int DepartmentCount { get; set; }
        public int TeamCount { get; set; }
        public string? AdminEmail { get; set; }
        public string? AdminName { get; set; }
        public string? BillingEmail { get; set; }
        public string? InternalNotes { get; set; }
        public int? MaxUsers { get; set; }
        public int? MaxDevices { get; set; }
        public int? StorageLimitGb { get; set; }
        public DateTime? TrialEndsAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SuspendedAt { get; set; }
        public string? SuspendReason { get; set; }
        public string? SuspensionType { get; set; }
        public DateTime? SuspendUntil { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public BrandingDTO? Branding { get; set; }
    }

    public class BrandingDTO
    {
        public string? LogoUrl { get; set; }
        public string? DisplayName { get; set; }
        public string? PrimaryColor { get; set; }
        public string? SecondaryColor { get; set; }
        public string? AccentColor { get; set; }
        public string? FontFamily { get; set; }
    }

    public class UpdateBrandingDTO
    {
        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        [MaxLength(100)]
        public string? DisplayName { get; set; }

        [MaxLength(20)]
        public string? PrimaryColor { get; set; }

        [MaxLength(20)]
        public string? SecondaryColor { get; set; }

        [MaxLength(20)]
        public string? AccentColor { get; set; }

        [MaxLength(100)]
        public string? FontFamily { get; set; }
    }

    public class TenantStatsDTO
    {
        public Guid OrganizationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalDevices { get; set; }
        public int OnlineDevices { get; set; }
        public int TotalScreenshots { get; set; }
        public int TotalTeams { get; set; }
        public int TotalDepartments { get; set; }
        public DateTime? LastActivityAt { get; set; }
    }

    // ── Requests ─────────────────────────────────────────────────────────────
    public class SuspendTenantDTO
    {
        [Required][MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        // PaymentOverdue | PolicyViolation | Fraud | RequestedByClient | Other
        [MaxLength(50)]
        public string Type { get; set; } = "Other";

        public DateTime? SuspendUntil { get; set; }

        public bool NotifyAdmin { get; set; } = true;
    }

    public class UpdatePlanDTO
    {
        [Required][MaxLength(50)]
        public string Plan { get; set; } = string.Empty;

        public bool ApplyImmediately { get; set; } = true;

        [MaxLength(500)]
        public string? BillingNote { get; set; }

        public bool SendNotification { get; set; } = true;
    }

    public class CreateTenantDTO
    {
        [Required][MaxLength(100)]
        public string OrganizationName { get; set; } = string.Empty;

        [Required][MaxLength(100)]
        public string AdminFirstName { get; set; } = string.Empty;

        [Required][MaxLength(100)]
        public string AdminLastName { get; set; } = string.Empty;

        [Required][EmailAddress]
        public string AdminEmail { get; set; } = string.Empty;

        [Required][MinLength(8)]
        public string AdminPassword { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Plan { get; set; } = "Free";

        public int? MaxUsers { get; set; }
        public int? StorageLimitGb { get; set; }

        public DateTime? TrialEndsAt { get; set; }

        [MaxLength(1000)]
        public string? InternalNotes { get; set; }

        public bool SendWelcomeEmail { get; set; } = true;
    }

    public class UpdateTenantDTO
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        [MaxLength(50)]
        public string? Plan { get; set; }

        public int? MaxUsers { get; set; }
        public int? MaxDevices { get; set; }
        public int? StorageLimitGb { get; set; }

        [MaxLength(1000)]
        public string? InternalNotes { get; set; }

        [MaxLength(200)]
        public string? BillingEmail { get; set; }

        public DateTime? TrialEndsAt { get; set; }
    }

    public class TenantFilterDTO
    {
        public string? Search { get; set; }
        public string? Plan { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int Limit { get; set; } = 20;

        // CreatedAt | Name | Plan | UserCount | LastActivity
        public string SortBy { get; set; } = "CreatedAt";
        public string Order { get; set; } = "desc";
    }

    public class GlobalMetricsDTO
    {
        public int TotalTenants { get; set; }
        public int ActiveTenants { get; set; }
        public int SuspendedTenants { get; set; }
        public int TrialTenants { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
    }
}
