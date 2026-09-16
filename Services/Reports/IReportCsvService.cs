using WiseMonitor.Api.DTOs.Reports;

namespace WiseMonitor.Api.Services.Reports;

public interface IReportCsvService
{
    byte[] Generate(
        ReportResponseDTO report);
}
