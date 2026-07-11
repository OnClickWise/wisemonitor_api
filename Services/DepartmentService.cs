using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs.Department;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public class DepartmentService : IDepartmentService
    {
        private readonly AppDbContext _context;

        private static readonly HashSet<string> ValidRoles =
            new(StringComparer.OrdinalIgnoreCase) { "DIRECTOR", "SUPERVISOR", "MEMBER", "GUEST" };

        public DepartmentService(AppDbContext context) => _context = context;

        public async Task<DepartmentDTO> CreateAsync(CreateDepartmentDTO dto, Guid organizationId)
        {
            var department = new Department
            {
                OrganizationId = organizationId,
                Name = dto.Name,
                Description = dto.Description,
            };

            _context.Departments.Add(department);
            await _context.SaveChangesAsync();

            if (dto.DirectorUserId.HasValue)
            {
                department.Members.Add(new DepartmentMember
                {
                    DepartmentId = department.Id,
                    UserId = dto.DirectorUserId.Value,
                    MemberRole = "DIRECTOR"
                });
                await _context.SaveChangesAsync();
            }

            return await BuildDTOAsync(department.Id, organizationId);
        }

        public async Task<List<DepartmentDTO>> GetAllAsync(Guid organizationId)
        {
            var departments = await _context.Departments
                .AsNoTracking()
                .Include(d => d.Members).ThenInclude(m => m.User)
                .Include(d => d.Teams)
                .Where(d => d.OrganizationId == organizationId)
                .OrderBy(d => d.Name)
                .ToListAsync();

            return departments.Select(MapToDTO).ToList();
        }

        public async Task<DepartmentDTO?> GetByIdAsync(Guid id, Guid organizationId)
        {
            var department = await _context.Departments
                .AsNoTracking()
                .Include(d => d.Members).ThenInclude(m => m.User)
                .Include(d => d.Teams)
                .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == organizationId);

            return department == null ? null : MapToDTO(department);
        }

        public async Task<DepartmentDTO> UpdateAsync(Guid id, UpdateDepartmentDTO dto, Guid organizationId)
        {
            var department = await _context.Departments
                .Include(d => d.Members).ThenInclude(m => m.User)
                .Include(d => d.Teams)
                .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == organizationId)
                ?? throw new KeyNotFoundException("Departamento não encontrado.");

            if (dto.Name != null) department.Name = dto.Name;
            if (dto.Description != null) department.Description = dto.Description;
            if (dto.IsActive.HasValue) department.IsActive = dto.IsActive.Value;
            department.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return MapToDTO(department);
        }

        public async Task DeleteAsync(Guid id, Guid organizationId)
        {
            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.Id == id && d.OrganizationId == organizationId)
                ?? throw new KeyNotFoundException("Departamento não encontrado.");

            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();
        }

        public async Task AddMemberAsync(Guid departmentId, AddDepartmentMemberDTO dto, Guid organizationId)
        {
            var normalizedRole = dto.MemberRole?.ToUpper() ?? "MEMBER";
            if (!ValidRoles.Contains(normalizedRole))
                throw new ArgumentException($"Papel inválido: {dto.MemberRole}. Use DIRECTOR, SUPERVISOR, MEMBER ou GUEST.");

            var department = await _context.Departments
                .Include(d => d.Members)
                .FirstOrDefaultAsync(d => d.Id == departmentId && d.OrganizationId == organizationId)
                ?? throw new KeyNotFoundException("Departamento não encontrado.");

            var userExists = await _context.Users
                .AnyAsync(u => u.Id == dto.UserId && u.OrganizationId == organizationId);

            if (!userExists)
                throw new KeyNotFoundException("Usuário não encontrado na organização.");

            // If role is DIRECTOR, enforce single director rule
            if (normalizedRole == "DIRECTOR")
            {
                var existingDirector = department.Members.FirstOrDefault(m => m.MemberRole == "DIRECTOR");
                if (existingDirector != null)
                {
                    if (!dto.ForceReplaceDirector)
                        throw new InvalidOperationException("Já existe um diretor neste departamento. Use ForceReplaceDirector=true para substituir.");
                    existingDirector.MemberRole = "MEMBER";
                }
            }

            var existing = department.Members.FirstOrDefault(m => m.UserId == dto.UserId);
            if (existing != null)
            {
                existing.MemberRole = normalizedRole;
            }
            else
            {
                department.Members.Add(new DepartmentMember
                {
                    DepartmentId = departmentId,
                    UserId = dto.UserId,
                    MemberRole = normalizedRole,
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task RemoveMemberAsync(Guid departmentId, Guid userId, Guid organizationId)
        {
            var department = await _context.Departments
                .Include(d => d.Members)
                .FirstOrDefaultAsync(d => d.Id == departmentId && d.OrganizationId == organizationId)
                ?? throw new KeyNotFoundException("Departamento não encontrado.");

            var member = department.Members.FirstOrDefault(m => m.UserId == userId)
                ?? throw new KeyNotFoundException("Usuário não é membro deste departamento.");

            department.Members.Remove(member);
            await _context.SaveChangesAsync();
        }

        public async Task<List<DepartmentMemberDTO>> GetMembersAsync(Guid departmentId, Guid organizationId)
        {
            var members = await _context.DepartmentMembers
                .AsNoTracking()
                .Include(m => m.User)
                .Where(m => m.DepartmentId == departmentId && m.Department.OrganizationId == organizationId)
                .OrderBy(m => m.User.FullName)
                .ToListAsync();

            return members.Select(MapMemberToDTO).ToList();
        }

        public async Task<List<DepartmentSupervisorDTO>> GetSupervisorsAsync(Guid departmentId, Guid organizationId)
        {
            var supervisors = await _context.DepartmentMembers
                .AsNoTracking()
                .Include(m => m.User)
                .Where(m => m.DepartmentId == departmentId
                    && m.Department.OrganizationId == organizationId
                    && m.MemberRole == "SUPERVISOR")
                .OrderBy(m => m.User.FullName)
                .ToListAsync();

            return supervisors.Select(m => new DepartmentSupervisorDTO
            {
                UserId = m.UserId,
                FullName = m.User?.FullName ?? string.Empty,
                Email = m.User?.Email ?? string.Empty
            }).ToList();
        }

        public async Task UpdateMemberRoleAsync(Guid departmentId, Guid userId, UpdateMemberRoleDTO dto, Guid organizationId)
        {
            var normalizedRole = dto.MemberRole?.ToUpper() ?? "MEMBER";
            if (!ValidRoles.Contains(normalizedRole))
                throw new ArgumentException($"Papel inválido: {dto.MemberRole}.");

            var department = await _context.Departments
                .Include(d => d.Members)
                .FirstOrDefaultAsync(d => d.Id == departmentId && d.OrganizationId == organizationId)
                ?? throw new KeyNotFoundException("Departamento não encontrado.");

            var member = department.Members.FirstOrDefault(m => m.UserId == userId)
                ?? throw new KeyNotFoundException("Usuário não é membro deste departamento.");

            if (normalizedRole == "DIRECTOR")
            {
                var existingDirector = department.Members.FirstOrDefault(m => m.MemberRole == "DIRECTOR" && m.UserId != userId);
                if (existingDirector != null)
                {
                    if (!dto.ForceReplaceDirector)
                        throw new InvalidOperationException("Já existe um diretor neste departamento. Use ForceReplaceDirector=true para substituir.");
                    existingDirector.MemberRole = "MEMBER";
                }
            }

            member.MemberRole = normalizedRole;
            await _context.SaveChangesAsync();
        }

        public async Task SetDirectorAsync(Guid departmentId, SetDirectorDTO dto, Guid organizationId)
        {
            var department = await _context.Departments
                .Include(d => d.Members)
                .FirstOrDefaultAsync(d => d.Id == departmentId && d.OrganizationId == organizationId)
                ?? throw new KeyNotFoundException("Departamento não encontrado.");

            var existingDirector = department.Members.FirstOrDefault(m => m.MemberRole == "DIRECTOR");
            if (existingDirector != null)
            {
                if (!dto.ForceReplace)
                    throw new InvalidOperationException("Já existe um diretor neste departamento. Use ForceReplace=true para substituir.");
                existingDirector.MemberRole = "MEMBER";
            }

            var targetMember = department.Members.FirstOrDefault(m => m.UserId == dto.UserId);
            if (targetMember != null)
            {
                targetMember.MemberRole = "DIRECTOR";
            }
            else
            {
                var userExists = await _context.Users
                    .AnyAsync(u => u.Id == dto.UserId && u.OrganizationId == organizationId);
                if (!userExists)
                    throw new KeyNotFoundException("Usuário não encontrado na organização.");

                department.Members.Add(new DepartmentMember
                {
                    DepartmentId = departmentId,
                    UserId = dto.UserId,
                    MemberRole = "DIRECTOR"
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task<DepartmentOverviewDTO> GetOverviewAsync(Guid departmentId, Guid organizationId)
        {
            var department = await _context.Departments
                .AsNoTracking()
                .Include(d => d.Members).ThenInclude(m => m.User)
                .Include(d => d.Teams).ThenInclude(t => t.Manager)
                .Include(d => d.Teams).ThenInclude(t => t.Members)
                .FirstOrDefaultAsync(d => d.Id == departmentId && d.OrganizationId == organizationId)
                ?? throw new KeyNotFoundException("Departamento não encontrado.");

            var director = department.Members.FirstOrDefault(m => m.MemberRole == "DIRECTOR");
            var supervisors = department.Members.Where(m => m.MemberRole == "SUPERVISOR").ToList();

            var teamIds = department.Teams.Select(t => t.Id).ToList();
            var schedules = await _context.WorkSchedules
                .AsNoTracking()
                .Where(s => s.OrganizationId == organizationId)
                .ToListAsync();

            return new DepartmentOverviewDTO
            {
                Id = department.Id,
                Name = department.Name,
                Description = department.Description,
                IsActive = department.IsActive,
                CreatedAt = department.CreatedAt,
                MemberCount = department.Members.Count,
                TeamCount = department.Teams.Count,
                Director = director == null ? null : MapMemberToDTO(director),
                Supervisors = supervisors.Select(m => new DepartmentSupervisorDTO
                {
                    UserId = m.UserId,
                    FullName = m.User?.FullName ?? string.Empty,
                    Email = m.User?.Email ?? string.Empty
                }).ToList(),
                Teams = department.Teams.Select(t => new DepartmentTeamSummaryDTO
                {
                    Id = t.Id,
                    Name = t.Name,
                    MemberCount = t.Members.Count,
                    ManagerName = t.Manager?.FullName ?? string.Empty
                }).ToList(),
                Schedules = schedules.Select(s => new DepartmentScheduleSummaryDTO
                {
                    Id = s.Id,
                    Name = s.Name,
                    TypeLabel = s.Type switch
                    {
                        Models.WorkScheduleType.Fixed => "Horário Fixo",
                        Models.WorkScheduleType.FiveTwo => "5x2",
                        Models.WorkScheduleType.SixOne => "6x1",
                        Models.WorkScheduleType.TwelveThirtySix => "12x36",
                        Models.WorkScheduleType.Supervision => "Supervisão",
                        Models.WorkScheduleType.Custom => "Personalizado",
                        _ => "Desconhecido"
                    }
                }).ToList()
            };
        }

        private static DepartmentDTO MapToDTO(Department d)
        {
            var director = d.Members.FirstOrDefault(m => m.MemberRole == "DIRECTOR");
            return new DepartmentDTO
            {
                Id = d.Id,
                OrganizationId = d.OrganizationId,
                Name = d.Name,
                Description = d.Description,
                IsActive = d.IsActive,
                CreatedAt = d.CreatedAt,
                MemberCount = d.Members.Count,
                TeamCount = d.Teams.Count,
                Director = director == null ? null : MapMemberToDTO(director),
                Members = d.Members.Select(MapMemberToDTO).ToList()
            };
        }

        private static DepartmentMemberDTO MapMemberToDTO(DepartmentMember m) => new()
        {
            UserId = m.UserId,
            FullName = m.User?.FullName ?? string.Empty,
            Email = m.User?.Email ?? string.Empty,
            Role = m.User?.Role ?? string.Empty,
            MemberRole = m.MemberRole,
            JoinedAt = m.JoinedAt
        };

        private async Task<DepartmentDTO> BuildDTOAsync(Guid id, Guid organizationId)
        {
            var d = await _context.Departments
                .AsNoTracking()
                .Include(d => d.Members).ThenInclude(m => m.User)
                .Include(d => d.Teams)
                .FirstOrDefaultAsync(dep => dep.Id == id && dep.OrganizationId == organizationId)
                ?? throw new KeyNotFoundException("Departamento não encontrado.");
            return MapToDTO(d);
        }
    }
}
