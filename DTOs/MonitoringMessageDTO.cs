using System;

namespace WiseMonitor.Api.DTOs
{
    public class MonitoringMessageDto
    {
        public string OrgId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;

       
        // ✅ AGORA OPCIONAIS (CORREÇÃO REAL)
        public string? ThumbnailUrl { get; set; }
        public string? FullScreenUrl { get; set; }

        public string Status { get; set; } = "online"; // online/offline
        public string Type { get; set; } = string.Empty; // ex: "screenshot", "activity", "appFocus"
        public string Payload { get; set; } = string.Empty; // JSON, base64 ou texto

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
