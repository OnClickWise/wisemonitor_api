using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WiseMonitor.Api.Models
{
    /// <summary>
    /// Add-on contratado por uma organização (AI_INSIGHTS, EXTENDED_STORAGE, DLP, ...).
    /// Concede features extras sem trocar de plano — ver PlanCatalog.AddOnFeatures.
    /// </summary>
    public class OrganizationAddOn
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OrganizationId { get; set; }

        [ForeignKey(nameof(OrganizationId))]
        public Organization? Organization { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        // Para EXTENDED_STORAGE: dias adicionais de retenção. Para os demais, normalmente 1.
        public int Quantity { get; set; } = 1;

        [Column(TypeName = "numeric(10,2)")]
        public decimal? UnitPriceUsd { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndsAt { get; set; }
    }
}
