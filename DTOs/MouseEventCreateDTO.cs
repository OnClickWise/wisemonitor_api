using System;

namespace WiseMonitor.Api.DTOs
{
    public class MouseEventCreateDTO
    {
        public Guid SessionId { get; set; }

        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }

        public string? Application { get; set; }

        public MouseMetricsDTO? Metrics { get; set; }
    }
}
