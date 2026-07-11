using System;

namespace WiseMonitor.Api.DTOs
{
    public class AppFocusEventUpdateDTO
    {
        public string ApplicationName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;

        public string? Url { get; set; }
        public string? FaviconUrl { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}
