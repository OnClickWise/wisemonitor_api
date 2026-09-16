namespace WiseMonitor.Api.DTOs.Reports;

public class ReportFilterDTO
{
    public Guid? TeamId { get; set; }

    public Guid? UserId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }
}
