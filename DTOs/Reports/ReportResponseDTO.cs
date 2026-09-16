namespace WiseMonitor.Api.DTOs.Reports;

public class ReportResponseDTO
{
    public string CompanyName { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    public string SubjectType { get; set; } = string.Empty;

    public string SubjectName { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public long ProductiveSeconds { get; set; }

    public long NeutralSeconds { get; set; }

    public long UnproductiveSeconds { get; set; }

    public long IdleSeconds { get; set; }

    public long TotalSeconds { get; set; }

    public DateTime GeneratedAt { get; set; }

    public List<ReportApplicationDTO> TopApplications { get; set; } = [];

    public List<ReportDailyActivityDTO> DailyActivities { get; set; } = [];

    public List<ReportActivityDTO> Activities { get; set; } = [];
}
