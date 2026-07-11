using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.Models
{
    public class Department
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid OrganizationId { get; set; }
        public Organization Organization { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<DepartmentMember> Members { get; set; } = new List<DepartmentMember>();
        public ICollection<Team> Teams { get; set; } = new List<Team>();
    }
}
