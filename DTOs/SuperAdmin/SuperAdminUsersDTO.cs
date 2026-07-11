using System;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.DTOs.SuperAdmin
{
    public class SuperAdminUserResponseDTO
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsSuperAdmin { get; set; }
        public Guid? OrganizationId { get; set; }
        public string? OrganizationName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public class SuperAdminUserFilterDTO
    {
        public string? Search { get; set; }
        public Guid? OrganizationId { get; set; }
        public string? Role { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? LastLoginFrom { get; set; }
        public DateTime? LastLoginTo { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int Limit { get; set; } = 20;
    }

    public class PaginationDTO
    {
        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int Limit { get; set; } = 20;
    }

    public class CreateSuperAdminDTO
    {
        [Required][MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required][MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required][EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required][MinLength(8)]
        public string Password { get; set; } = string.Empty;
    }
}
