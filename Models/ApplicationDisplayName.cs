using System;

namespace WiseMonitor.Api.Models
{
    public class ApplicationDisplayName
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Organização dona desta configuração
        public Guid OrganizationId { get; set; }

        // Nome técnico identificado pelo Agent.
        // Exemplo: Code
        public string ApplicationName { get; set; } = string.Empty;

        // Nome personalizado que será exibido na tela.
        // Exemplo: Visual Studio Code
        public string DisplayName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
