using WiseMonitor.Api.DTOs.Reports;

namespace WiseMonitor.Api.Services.Reports;

public interface IReportPdfService
{
    Task<byte[]> GenerateAsync(
        ReportResponseDTO report);
}
