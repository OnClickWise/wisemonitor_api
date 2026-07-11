using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace WiseMonitor.Api.Models
{
    public class Team
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OrganizationId { get; set; }

        // Departamento ao qual a equipe pertence (opcional)
        public Guid? DepartmentId { get; set; }

        [ForeignKey("DepartmentId")]
        public Department? Department { get; set; }

        public Guid? DefaultWorkScheduleId { get; set; }

        [ForeignKey("DefaultWorkScheduleId")]
        public WorkSchedule? DefaultWorkSchedule { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public Guid ManagerId { get; set; }

        [ForeignKey("ManagerId")]
        public User Manager { get; set; } = null!;

        public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
