using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WiseMonitor.Api.Models
{
    public class Organization
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? LegalName { get; set; }

        [MaxLength(20)]
        public string? Cnpj {  get; set; }

        // Plano de assinatura: Free | Basic | Pro | Enterprise
        [MaxLength(50)]
        public string Plan { get; set; } = "Free";

        // Status: Active | Suspended | Cancelled | Trial
        [MaxLength(50)]
        public string Status { get; set; } = "Active";

        public Guid? AdminUserId { get; set; }

        [ForeignKey(nameof(AdminUserId))]
        public User? AdminUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SuspendedAt { get; set; }
        public string? SuspendReason { get; set; }

        // Tipo de suspensão: PaymentOverdue | PolicyViolation | Fraud | RequestedByClient | Other
        [MaxLength(50)]
        public string? SuspensionType { get; set; }
        public DateTime? SuspendUntil { get; set; }

        // Limites do plano
        public int? MaxUsers { get; set; }
        public int? MaxDevices { get; set; }
        public int? StorageLimitGb { get; set; }

        public DateTime? TrialEndsAt { get; set; }

        [MaxLength(1000)]
        public string? InternalNotes { get; set; }

        [MaxLength(200)]
        public string? BillingEmail { get; set; }

        // White-label branding
        [MaxLength(500)]
        public string? BrandingLogoUrl { get; set; }

        [MaxLength(100)]
        public string? BrandingDisplayName { get; set; }

        [MaxLength(20)]
        public string? BrandingPrimaryColor { get; set; }

        [MaxLength(20)]
        public string? BrandingSecondaryColor { get; set; }

        [MaxLength(20)]
        public string? BrandingAccentColor { get; set; }

        [MaxLength(100)]
        public string? BrandingFontFamily { get; set; }

        // Métricas cacheadas (atualizadas periodicamente)
        public int CachedUserCount { get; set; }
        public int CachedDeviceCount { get; set; }
        public double CachedStorageGb { get; set; }
        public DateTime? LastActivityAt { get; set; }

        public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Department> Departments { get; set; } = new List<Department>();
    }
}
