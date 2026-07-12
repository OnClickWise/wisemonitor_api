using System;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.DTOs
{
    public class UserUpdateDTO
    {
        [StringLength(50)]
        public string? FirstName { get; set; }

        [StringLength(50)]
        public string? LastName { get; set; }

        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        public string? Email { get; set; }

        public string? Role { get; set; }
        public string? Position { get; set; } // Cargo
        public bool? IsActive { get; set; }
        public Guid? OrganizationId { get; set; }
    }
}
