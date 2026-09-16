namespace WiseMonitor.Api.DTOs.Reports;

public class ReportActivityDTO
{
    public string ApplicationName { get; set; } = string.Empty;

    public string? Url { get; set; }

    public string Category { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public long DurationSeconds { get; set; }
}
