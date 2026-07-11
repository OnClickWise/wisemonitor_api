using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs.Team;

namespace WiseMonitor.Api.Services
{
    public interface ITeamService
    {
        // ===============================
        // 🟢 CREATE
        // ===============================
        Task CreateAsync(CreateTeamDTO dto, Guid organizationId);

        // ===============================
        // 🔵 READ
        // ===============================
        Task<List<TeamResponseDTO>> GetAllAsync(Guid organizationId);
        Task<TeamResponseDTO?> GetByIdAsync(Guid teamId, Guid organizationId);

        // ===============================
        // 🟡 UPDATE
        // ===============================
        Task UpdateAsync(Guid teamId, UpdateTeamDTO dto, Guid organizationId);

        // ===============================
        // 🔴 DELETE
        // ===============================
        Task DeleteAsync(Guid teamId, Guid organizationId);

        // ===============================
        // 👥 MEMBERS MANAGEMENT
        // ===============================
        Task AddMemberAsync(Guid teamId, Guid userId, Guid organizationId);
        Task RemoveMemberAsync(Guid teamId, Guid userId, Guid organizationId);
    }
}
