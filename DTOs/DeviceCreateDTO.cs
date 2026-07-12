using System;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.DTOs
{
    public class DeviceCreateDTO
    {
        [Required(ErrorMessage = "Hostname é obrigatório.")]
        [MaxLength(150)]
        public string Hostname { get; set; } = string.Empty;

        // Opcional — sem [EmailAddress] pois o valor padrão é string vazia
        // (o atributo rejeitaria "" por não ser um e-mail válido).
        public string AgentEmail { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Department { get; set; } = string.Empty;

        [MaxLength(45)]
        public string IpAddress { get; set; } = string.Empty;
    }
}
