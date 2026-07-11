using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.DTOs.Team
{
    public class UpdateTeamDTO
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        // 👤 Responsável pelo time (Admin ou Manager)
        [Required]
        public Guid? ManagerId { get; set; }

        public List<Guid> MemberIds { get; set; } = new();

        public Guid? WorkScheduleId { get; set; }
    }
}
