namespace WiseMonitor.Api.DTOs.Reports;

public class ReportDailyActivityDTO
{
    public DateTime Date { get; set; }

    public int ActivityCount { get; set; }

    public long TotalSeconds { get; set; }
}
