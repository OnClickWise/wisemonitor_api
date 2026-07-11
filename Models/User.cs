using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WiseMonitor.Api.Models.Enums;

namespace WiseMonitor.Api.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        public string FullName => $"{FirstName} {LastName}".Trim();

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? AvatarUrl { get; set; }

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        // Papel principal do usuário (usar constantes UserRoles)
        public string Role { get; set; } = UserRoles.Employee;

        // SuperAdmin é flag de plataforma, não de tenant
        public bool IsSuperAdmin { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // FK para organização (null quando SuperAdmin de plataforma)
        public Guid? OrganizationId { get; set; }

        [ForeignKey("OrganizationId")]
        public Organization? Organization { get; set; }

        // Departamentos (navegação reversa)
        public ICollection<DepartmentMember> DepartmentMemberships { get; set; } = new List<DepartmentMember>();

        // Equipes (navegação reversa)
        public ICollection<TeamMember> TeamMemberships { get; set; } = new List<TeamMember>();
    }
}
