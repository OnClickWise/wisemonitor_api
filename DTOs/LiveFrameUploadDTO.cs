using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace WiseMonitor.Api.DTOs
{
    // Frame de transmissão ao vivo — não é persistido no banco, apenas repassado
    // via WebSocket (/ws/monitor) para quem estiver assistindo o device.
    public class LiveFrameUploadDTO
    {
        [Required]
        public IFormFile Frame { get; set; } = default!;

        [Required]
        [MaxLength(200)]
        public string DeviceId { get; set; } = string.Empty;

        [Required]
        public Guid OrganizationId { get; set; }
    }
}
