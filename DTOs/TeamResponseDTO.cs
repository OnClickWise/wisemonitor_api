using System;
using System.Collections.Generic;

namespace WiseMonitor.Api.DTOs.Team
{
    public class TeamResponseDTO
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }

        public Guid ManagerId { get; set; }
        public string ManagerName { get; set; } = string.Empty;

        public List<TeamMemberDTO> Members { get; set; } = new();

        public Guid? WorkScheduleId { get; set; }

        public string? WorkScheduleName { get; set; }
    }

    public class TeamMemberDTO
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        
        public string Role { get; set; } = string.Empty;
    }
}
