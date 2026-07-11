using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace WiseMonitor.Api.DTOs
{
    public class KeyboardEventCreateDTO
    {
        public Guid SessionId { get; set; }

        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }

        public string? Application { get; set; }

        public KeyboardMetricsDTO? Metrics { get; set; }

        public List<string> Words { get; set; } = new();
    }
}