using System;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.Models
{
    public class AgentVersion
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // semver ex: "2.1.0"
        [Required][MaxLength(20)]
        public string Version { get; set; } = string.Empty;

        // Stable | Beta | Alpha | Deprecated
        [Required][MaxLength(20)]
        public string Channel { get; set; } = "Stable";

        public string ReleaseNotes { get; set; } = string.Empty;

        [MaxLength(64)]
        public string Checksum { get; set; } = string.Empty;

        public bool ForceUpdate { get; set; } = false;

        [MaxLength(20)]
        public string? MinimumVersion { get; set; }

        // Links por plataforma
        [MaxLength(500)]
        public string? WindowsDownloadUrl { get; set; }
        [MaxLength(64)]
        public string? WindowsChecksum { get; set; }

        [MaxLength(500)]
        public string? MacOsDownloadUrl { get; set; }
        [MaxLength(64)]
        public string? MacOsChecksum { get; set; }

        [MaxLength(500)]
        public string? LinuxDownloadUrl { get; set; }
        [MaxLength(64)]
        public string? LinuxChecksum { get; set; }

        public bool IsActive { get; set; } = true;

        public Guid PublishedByAdminId { get; set; }
        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    }
}
