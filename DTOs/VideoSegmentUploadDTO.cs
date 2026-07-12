using System;
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
}
