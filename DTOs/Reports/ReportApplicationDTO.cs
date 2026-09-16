namespace WiseMonitor.Api.DTOs.Reports;

public class ReportApplicationDTO
{
    public string Name { get; set; } = string.Empty;

    public long TotalSeconds { get; set; }

    public double Percentage { get; set; }
}
