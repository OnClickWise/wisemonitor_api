using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WiseMonitor.Api.Models
{
    public class OrganizationAppearanceSettings
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid OrganizationId { get; set; }

        [Required]
        [MaxLength(10)]
        public string Theme { get; set; } = "light";

        [ForeignKey(nameof(OrganizationId))]
        public Organization Organization { get; set; } = null!;

        // Fundo da página
        [MaxLength(20)]
        public string? PageBackgroundType { get; set; }

        [MaxLength(20)]
        public string? PageBackgroundColor { get; set; }

        [MaxLength(20)]
        public string? PageGradientStart { get; set; }

        [MaxLength(20)]
        public string? PageGradientEnd { get; set; }

        [MaxLength(30)]
        public string? PageGradientDirection { get; set; }

        // Sidebar
        [MaxLength(20)]
        public string? SidebarBackgroundType { get; set; }

        [MaxLength(20)]
        public string? SidebarColor { get; set; }

        [MaxLength(20)]
        public string? SidebarGradientStart { get; set; }

        [MaxLength(20)]
        public string? SidebarGradientEnd { get; set; }

        [MaxLength(30)]
        public string? SidebarGradientDirection { get; set; }

        [MaxLength(20)]
        public string? SidebarForegroundColor { get; set; }

        [MaxLength(20)]
        public string? SidebarActiveBackground { get; set; }

        [MaxLength(20)]
        public string? SidebarActiveForeground { get; set; }

        [MaxLength(20)]
        public string? SidebarHoverBackground { get; set; }

        [MaxLength(20)]
        public string? SidebarHoverForeground { get; set; }

        [MaxLength(20)]
        public string? SidebarProfileBackground { get; set; }

        // Cards
        [MaxLength(20)]
        public string? CardBackgroundType { get; set; }

        [MaxLength(20)]
        public string? CardBackgroundColor { get; set; }

        [MaxLength(20)]
        public string? CardGradientStart { get; set; }

        [MaxLength(20)]
        public string? CardGradientEnd { get; set; }

        [MaxLength(30)]
        public string? CardGradientDirection { get; set; }

        [MaxLength(20)]
        public string? CardPrimaryTextColor { get; set; }

        [MaxLength(20)]
        public string? CardSecondaryTextColor { get; set; }

        // Botões
        [MaxLength(20)]
        public string? ButtonBackgroundColor { get; set; }

        [MaxLength(20)]
        public string? ButtonForegroundColor { get; set; }

        [MaxLength(20)]
        public string? ButtonHoverBackgroundColor { get; set; }

        // Gráfico
        [MaxLength(20)]
        public string? ChartBackgroundType { get; set; }

        [MaxLength(20)]
        public string? ChartColor { get; set; }

        [MaxLength(20)]
        public string? ChartGradientStart { get; set; }

        [MaxLength(20)]
        public string? ChartGradientEnd { get; set; }

        [MaxLength(30)]
        public string? ChartGradientDirection { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
