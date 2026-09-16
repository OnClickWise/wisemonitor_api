using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace WiseMonitor.Api.Models
{
    public class ProductivityClassification
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OrganizationId { get; set; }

        public Guid TeamId { get; set; }

        [ForeignKey(nameof(TeamId))]
        public Team Team { get; set; } = null!;

        /// <summary>
        /// app:code
        /// site:github.com
        /// </summary>
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// Nome exibido para o usuário.
        /// Ex:
        /// Visual Studio Code
        /// github.com
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Aplicativo ou Site.
        /// </summary>
        public ProductivityItemType ItemType { get; set; }

        /// <summary>
        /// Produtivo / Neutro / Improdutivo.
        /// Reutiliza o enum já existente no sistema.
        /// </summary>
        public ActivityCategory Category { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
