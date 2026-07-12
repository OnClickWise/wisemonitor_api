using System;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.Models
{
    /// <summary>
    /// Um segmento curto (padrão ~10s) de vídeo real da tela do usuário monitorado.
    /// Serve tanto para a visualização "ao vivo" (o segmento mais recente de um device)
    /// quanto para o histórico (todos os segmentos dentro de uma janela de retenção).
    /// </summary>
    public class VideoSegment
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // 🔐 Multi-tenant
        [Required]
        public Guid OrganizationId { get; set; }

        // 👤 Usuário monitorado
        [Required]
        public Guid MonitoredUserId { get; set; }

        // 💻 Dispositivo que enviou
        [Required]
        [MaxLength(200)]
        public string DeviceId { get; set; } = string.Empty;

        // 🕒 Janela de tempo coberta por este segmento
        [Required]
        public DateTime StartedAt { get; set; }

        [Required]
        public DateTime EndedAt { get; set; }

        // 🎞️ Vídeo
        [Required]
        public byte[] VideoData { get; set; } = Array.Empty<byte>();

        [MaxLength(50)]
        public string ContentType { get; set; } = "video/mp4";

        public long SizeInBytes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
