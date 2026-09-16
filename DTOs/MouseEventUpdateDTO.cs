using System;

namespace WiseMonitor.Api.DTOs
{
    public class MouseEventUpdateDTO
    {
        public DateTime EndAt { get; set; }

        public int LeftClicks { get; set; }
        public int RightClicks { get; set; }
        public int MiddleClicks { get; set; }
        public int ScrollCount { get; set; }
    }
}
