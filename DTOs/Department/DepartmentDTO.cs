using System;
using System.Collections.Generic;

namespace WiseMonitor.Api.DTOs.Department
{
    public class DepartmentDTO
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int MemberCount { get; set; }
        public int TeamCount { get; set; }
        public DepartmentMemberDTO? Director { get; set; }
        public List<DepartmentMemberDTO> Members { get; set; } = new();
    }

    public class DepartmentMemberDTO
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string MemberRole { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
    }

    public class CreateDepartmentDTO
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? DirectorUserId { get; set; }
    }

    public class UpdateDepartmentDTO
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsActive { get; set; }
        public Guid? DirectorUserId { get; set; }
    }

    public class AddDepartmentMemberDTO
    {
        public Guid UserId { get; set; }
        public string MemberRole { get; set; } = "MEMBER";
        public bool ForceReplaceDirector { get; set; } = false;
    }

    public class UpdateMemberRoleDTO
    {
        public string MemberRole { get; set; } = "MEMBER";
        public bool ForceReplaceDirector { get; set; } = false;
    }

    public class SetDirectorDTO
    {
        public Guid UserId { get; set; }
        public bool ForceReplace { get; set; } = false;
    }

    public class DepartmentOverviewDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int MemberCount { get; set; }
        public int TeamCount { get; set; }
        public DepartmentMemberDTO? Director { get; set; }
        public List<DepartmentSupervisorDTO> Supervisors { get; set; } = new();
        public List<DepartmentTeamSummaryDTO> Teams { get; set; } = new();
        public List<DepartmentScheduleSummaryDTO> Schedules { get; set; } = new();
    }

    public class DepartmentSupervisorDTO
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class DepartmentTeamSummaryDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public string ManagerName { get; set; } = string.Empty;
    }

    public class DepartmentScheduleSummaryDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
    }

    public class TeamRefDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
