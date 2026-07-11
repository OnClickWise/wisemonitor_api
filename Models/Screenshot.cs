using System;
using System.ComponentModel.DataAnnotations;

namespace WiseMonitor.Api.Models
{
    public class Screenshot
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
        [MaxLength(200)]
        public string? DeviceId { get; set; }

        // 🕒 Momento da captura
        [Required]
        public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

        // 🖼️ Imagem principal
        [Required]
        public byte[] ImageData { get; set; } = Array.Empty<byte>();

        // 🧾 Metadados
        [MaxLength(50)]
        public string ContentType { get; set; } = "image/png";

        public long SizeInBytes { get; set; }

        // 🖼️ Thumbnail (uso futuro / opcional)
        public byte[]? ThumbnailData { get; set; }

        // 🧵 Concorrência otimista (EF Core)
        [Timestamp]
        public byte[]? RowVersion { get; set; }

        // 📎 Nome do arquivo (usado no download)
        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;
    }
}
