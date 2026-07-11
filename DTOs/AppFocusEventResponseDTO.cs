using System;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.DTOs
{
    public class AppFocusEventResponseDTO
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        public Guid DeviceId { get; set; }

        public string ApplicationName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;

        public string? Url { get; set; }
        public string? FaviconUrl { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }

        public long DurationSeconds { get; set; }

        public ActivityCategory Category { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
