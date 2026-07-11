using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Models.Enums;

namespace WiseMonitor.Api.Services
{
    public class SuperAdminAgentService : ISuperAdminAgentService
    {
        private readonly AppDbContext _context;
        private readonly IAuditService _audit;

        public SuperAdminAgentService(AppDbContext context, IAuditService audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IEnumerable<AgentVersionResponseDTO>> GetVersionsAsync(CancellationToken ct = default)
        {
            return await _context.AgentVersions.AsNoTracking()
                .OrderByDescending(v => v.PublishedAt)
                .Select(v => MapToDTO(v))
                .ToListAsync(ct);
        }

        public async Task<AgentVersionResponseDTO> PublishVersionAsync(PublishAgentVersionDTO dto, Guid adminId, CancellationToken ct = default)
        {
            if (await _context.AgentVersions.AnyAsync(v => v.Version == dto.Version, ct))
                throw new InvalidOperationException($"Versão '{dto.Version}' já existe.");

            var version = new AgentVersion
            {
                Version            = dto.Version,
                Channel            = dto.Channel,
                ReleaseNotes       = dto.ReleaseNotes,
                Checksum           = dto.Checksum,
                ForceUpdate        = dto.ForceUpdate,
                MinimumVersion     = dto.MinimumVersion,
                WindowsDownloadUrl = dto.WindowsDownloadUrl,
                WindowsChecksum    = dto.WindowsChecksum,
                MacOsDownloadUrl   = dto.MacOsDownloadUrl,
                MacOsChecksum      = dto.MacOsChecksum,
                LinuxDownloadUrl   = dto.LinuxDownloadUrl,
                LinuxChecksum      = dto.LinuxChecksum,
                PublishedByAdminId = adminId,
                IsActive           = true,
            };

            _context.AgentVersions.Add(version);
            await _context.SaveChangesAsync(ct);

            await _audit.LogAsync(
                action: "agent_version_publish",
                entityType: "AgentVersion",
                entityId: version.Id.ToString(),
                details: $"Versão do agente '{dto.Version}' ({dto.Channel}) publicada.",
                userId: adminId,
                userRole: UserRoles.SuperAdmin);

            return MapToDTO(version);
        }

        public async Task UpdateChannelAsync(Guid id, string channel, CancellationToken ct = default)
        {
            var version = await _context.AgentVersions.FindAsync(new object[] { id }, ct)
                ?? throw new KeyNotFoundException("Versão do agente não encontrada.");

            version.Channel = channel;
            if (channel == "Deprecated") version.IsActive = false;

            await _context.SaveChangesAsync(ct);
        }

        public async Task<int> ForceUpdateAsync(Guid versionId, ForceAgentUpdateDTO dto, CancellationToken ct = default)
        {
            var version = await _context.AgentVersions.FindAsync(new object[] { versionId }, ct)
                ?? throw new KeyNotFoundException("Versão do agente não encontrada.");

            version.ForceUpdate = true;
            await _context.SaveChangesAsync(ct);

            // Contagem de dispositivos afetados
            var query = _context.Devices.AsQueryable();
            if (dto.TargetOrganizationId.HasValue)
                query = query.Where(d => d.OrganizationId == dto.TargetOrganizationId.Value);

            return await query.CountAsync(ct);
        }

        public async Task<IEnumerable<AgentVersionDistributionDTO>> GetVersionDistributionAsync(CancellationToken ct = default)
        {
            var total = await _context.Devices.CountAsync(ct);
            if (total == 0) return [];

            var groups = await _context.Devices.AsNoTracking()
                .GroupBy(d => d.AgentVersion ?? "unknown")
                .Select(g => new { Version = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            return groups.Select(g => new AgentVersionDistributionDTO
            {
                Version    = g.Version,
                DeviceCount = g.Count,
                Percentage = Math.Round((double)g.Count / total * 100, 1),
            });
        }

        private static AgentVersionResponseDTO MapToDTO(AgentVersion v) => new()
        {
            Id                = v.Id,
            Version           = v.Version,
            Channel           = v.Channel,
            ReleaseNotes      = v.ReleaseNotes,
            Checksum          = v.Checksum,
            ForceUpdate       = v.ForceUpdate,
            MinimumVersion    = v.MinimumVersion,
            WindowsDownloadUrl = v.WindowsDownloadUrl,
            MacOsDownloadUrl  = v.MacOsDownloadUrl,
            LinuxDownloadUrl  = v.LinuxDownloadUrl,
            IsActive          = v.IsActive,
            PublishedAt       = v.PublishedAt,
        };
    }
}
