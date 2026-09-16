using WiseMonitor.Api.DTOs.Reports;

namespace WiseMonitor.Api.Services.Reports;

public interface IReportDataService
{
    Task<ReportResponseDTO> GetReportAsync(
        ReportFilterDTO filter,
        Guid organizationId,
        Guid callerId,
        string callerRole);
}
