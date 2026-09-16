using System;

namespace WiseMonitor.Api.Models
{
    public class MouseSession
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid OrganizationId { get; set; }

        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }

        public string? Application { get; set; }

        public int LeftClicks { get; set; }
        public int RightClicks { get; set; }
        public int MiddleClicks { get; set; }
        public int ScrollCount { get; set; }
    }
}
