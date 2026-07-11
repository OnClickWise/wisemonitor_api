using System;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.DTOs.SuperAdmin
{
    public class AgentVersionResponseDTO
    {
        public Guid Id { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string Checksum { get; set; } = string.Empty;
        public bool ForceUpdate { get; set; }
        public string? MinimumVersion { get; set; }
        public string? WindowsDownloadUrl { get; set; }
        public string? MacOsDownloadUrl { get; set; }
        public string? LinuxDownloadUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime PublishedAt { get; set; }
    }

    public class PublishAgentVersionDTO
    {
        [Required][MaxLength(20)]
        public string Version { get; set; } = string.Empty;

        // Stable | Beta | Alpha
        [MaxLength(20)]
        public string Channel { get; set; } = "Stable";

        public string ReleaseNotes { get; set; } = string.Empty;

        [MaxLength(64)]
        public string Checksum { get; set; } = string.Empty;

        public bool ForceUpdate { get; set; } = false;

        [MaxLength(20)]
        public string? MinimumVersion { get; set; }

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
    }

    public class UpdateAgentChannelDTO
    {
        [Required][MaxLength(20)]
        public string Channel { get; set; } = "Stable";
    }

    public class ForceAgentUpdateDTO
    {
        [MaxLength(500)]
        public string? Message { get; set; }
        public Guid? TargetOrganizationId { get; set; }
    }

    public class AgentVersionDistributionDTO
    {
        public string Version { get; set; } = string.Empty;
        public int DeviceCount { get; set; }
        public double Percentage { get; set; }
    }
}
