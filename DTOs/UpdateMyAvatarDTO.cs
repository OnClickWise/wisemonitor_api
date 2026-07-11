using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.DTOs
{
    public class UpdateMyAvatarDTO
    {
        [MaxLength(500)]
        public string? AvatarUrl { get; set; }
    }
}