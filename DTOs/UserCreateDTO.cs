using System.ComponentModel.DataAnnotations;
using WiseMonitor.Api.Validators;

namespace WiseMonitor.Api.DTOs
{
    public class UserCreateDTO
    {
        [Required]
        [StringLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string LastName { get; set; } = string.Empty;

        /*[Required]
        [StringLength(30)]
        public string UserName { get; set; } = string.Empty;*/

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        [StrongPassword]
        public string Password { get; set; } = string.Empty;

        public Guid OrganizationId { get; set; }

        [Required]
        public string Role { get; set; } = "User";

        
    }
}