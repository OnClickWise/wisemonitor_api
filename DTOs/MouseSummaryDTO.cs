namespace WiseMonitor.Api.DTOs
{
    public class MouseSummaryDTO
    {
        public int TotalLeftClicks { get; set; }
        public int TotalRightClicks { get; set; }
        public int TotalMiddleClicks { get; set; }
        public int TotalScrollCount { get; set; }
    }
}
