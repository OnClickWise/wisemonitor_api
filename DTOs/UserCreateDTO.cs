using System.ComponentModel.DataAnnotations;

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

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        public Guid OrganizationId { get; set; }

        [Required]
        public string Role { get; set; } = "User";

        
    }
}