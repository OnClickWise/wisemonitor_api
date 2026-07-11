using System;
using System.Collections.Generic;

namespace WiseMonitor.Api.DTOs.Team
{
    public class CreateTeamDTO
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        // UserId de Admin ou Manager
        public Guid ManagerId { get; set; }

        // UserIds da mesma organização
        public List<Guid> MemberIds { get; set; } = new();

        public Guid? DefaultWorkScheduleId { get; set; }

        public Guid? WorkScheduleId { get; set; }
    }
}
