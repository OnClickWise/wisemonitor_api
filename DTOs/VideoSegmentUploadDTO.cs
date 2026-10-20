using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace WiseMonitor.Api.DTOs
{
    public class VideoSegmentUploadDTO
    {
        public IFormFile Segment { get; set; } = default!;
        public string DeviceId { get; set; } = string.Empty;
        public Guid OrganizationId { get; set; }
        public Guid MonitoredUserId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
    }

    /// <summary>Resposta de POST upload-url: para onde o agent manda o MP4 (PUT).</summary>
    public class VideoSegmentUploadTicketDTO
    {
        public Guid SegmentId { get; set; }
        public string UploadUrl { get; set; } = string.Empty;
        public string ContentType { get; set; } = "video/mp4";
    }

    /// <summary>Enviado pelo agent depois que o PUT no bucket deu certo.</summary>
    public class VideoSegmentConfirmDTO
    {
        [Required]
        public Guid SegmentId { get; set; }

        [Required]
        [MaxLength(200)]
        public string DeviceId { get; set; } = string.Empty;

        [Required]
        public Guid MonitoredUserId { get; set; }

        [Required]
        public DateTime StartedAt { get; set; }

        [Required]
        public DateTime EndedAt { get; set; }

        public long SizeInBytes { get; set; }
    }
}
