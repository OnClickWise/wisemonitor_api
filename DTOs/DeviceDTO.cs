using System;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.DTOs
{
    /// <summary>
    /// Resposta dos endpoints de dispositivo. Antes eles devolviam a entidade, que traz a
    /// navegação User: quando o usuário já estava carregado na requisição, ia junto (com
    /// PasswordHash) ou a serialização quebrava num ciclo User → equipes → User.
    /// </summary>
    public class DeviceDto
    {
        public Guid Id { get; set; }
        public string Hostname { get; set; } = string.Empty;
        public string AgentEmail { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public string? AgentVersion { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid OrganizationId { get; set; }
        public Guid? UserId { get; set; }

        public static DeviceDto From(Device d) => new()
        {
            Id = d.Id,
            Hostname = d.Hostname,
            AgentEmail = d.AgentEmail,
            Department = d.Department,
            IpAddress = d.IpAddress,
            IsOnline = d.IsOnline,
            AgentVersion = d.AgentVersion,
            LastSeen = d.LastSeen,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            OrganizationId = d.OrganizationId,
            UserId = d.UserId,
        };
    }
}
