using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WiseMonitor.Api.Models
{
    [Table("Devices")]
    public class Device
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(150)]
        public string Hostname { get; set; } = string.Empty;

        [MaxLength(150)]
        public string AgentEmail { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Department { get; set; } = string.Empty;

        [MaxLength(45)] // IPv4 ou IPv6
        public string IpAddress { get; set; } = string.Empty;

        public bool IsOnline { get; set; } = false;

        [MaxLength(20)]
        public string? AgentVersion { get; set; }

        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Multi-tenant
        [Required]
        public Guid OrganizationId { get; set; }
    }
}
