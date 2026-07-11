using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiseMonitor.Api.DTOs
{
    public class AppFocusEventCreateDTO
    {
        public Guid DeviceId { get; set; }
        public string ApplicationName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        
        // 🌐 NOVOS CAMPOS
        public string? Url { get; set; }
        public string? FaviconUrl { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
}