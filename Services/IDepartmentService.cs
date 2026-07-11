using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs.Department;

namespace WiseMonitor.Api.Services
{
    public interface IDepartmentService
    {
        Task<DepartmentDTO> CreateAsync(CreateDepartmentDTO dto, Guid organizationId);
        Task<List<DepartmentDTO>> GetAllAsync(Guid organizationId);
        Task<DepartmentDTO?> GetByIdAsync(Guid id, Guid organizationId);
        Task<DepartmentDTO> UpdateAsync(Guid id, UpdateDepartmentDTO dto, Guid organizationId);
        Task DeleteAsync(Guid id, Guid organizationId);
        Task AddMemberAsync(Guid departmentId, AddDepartmentMemberDTO dto, Guid organizationId);
        Task RemoveMemberAsync(Guid departmentId, Guid userId, Guid organizationId);
        Task<List<DepartmentMemberDTO>> GetMembersAsync(Guid departmentId, Guid organizationId);
        Task<List<DepartmentSupervisorDTO>> GetSupervisorsAsync(Guid departmentId, Guid organizationId);
        Task UpdateMemberRoleAsync(Guid departmentId, Guid userId, UpdateMemberRoleDTO dto, Guid organizationId);
        Task SetDirectorAsync(Guid departmentId, SetDirectorDTO dto, Guid organizationId);
        Task<DepartmentOverviewDTO> GetOverviewAsync(Guid departmentId, Guid organizationId);
    }
}
