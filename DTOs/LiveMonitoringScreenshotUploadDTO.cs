using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using WiseMonitor.Api.DTOs;

namespace WiseMonitor.Api.DTOs
{
    public class LiveMonitoringScreenshotUploadDTO
    {
        [Required]
        public IFormFile Screenshot { get; set; } = default!;

        // Id do dispositivo que enviou a screenshot (string)
        [Required]
        [MaxLength(200)]
        public string DeviceId { get; set; } = string.Empty;

        // Id da organização (GUID)
        [Required]
        public Guid OrganizationId { get; set; }

        // Id do usuário monitorado — esse campo é o que o backend usa (GUID)
        [Required]
        public Guid MonitoredUserId { get; set; }

        // Opcional — campo auxiliar (não usado pelo backend no novo fluxo)
        public string BaseUrl { get; set; } = string.Empty;

        // Mantido apenas por compatibilidade; pode remover depois
        [MaxLength(200)]
        public string? UserId { get; set; }
    }
}
