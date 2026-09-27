using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.DTOs.Reports;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Models.Billing;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Services.Reports;

namespace WiseMonitor.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportDataService _reportDataService;
    private readonly IReportPdfService _reportPdfService;
    private readonly IReportCsvService _reportCsvService;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        IReportDataService reportDataService,
        IReportPdfService reportPdfService,
        IReportCsvService reportCsvService,
        ILogger<ReportsController> logger)
    {
        _reportDataService = reportDataService;
        _reportPdfService = reportPdfService;
        _reportCsvService = reportCsvService;
        _logger = logger;
    }

    [HttpPost("pdf")]
    [HasPermission(Permissions.ReportsExport)]
    [RequiresFeature(Features.ReportsExport)]
    public async Task<IActionResult> GeneratePdf(
        [FromBody] ReportFilterDTO filter)
    {
        var step = "Início";

        try
        {
            step = "Gerando dados do relatório";

            var report = await _reportDataService.GetReportAsync(
                filter,
                User.GetOrganizationId(),
                User.GetUserId(),
                User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty);

            step = "Gerando PDF";

            var pdfBytes = await _reportPdfService.GenerateAsync(report);

            step = "Montando nome do arquivo";

            var fileName = BuildFileName(
                report.SubjectName,
                report.StartDate,
                report.EndDate,
                "pdf");

            return File(
                pdfBytes,
                "application/pdf",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Reports] Falha ao gerar PDF na etapa {Step}", step);

            return StatusCode(500, new
            {
                Step = step,
                Exception = ex.GetType().FullName,
                Message = ex.Message,
                InnerException = ex.InnerException?.Message,
                StackTrace = ex.StackTrace
            });
        }
    }

    [HttpPost("csv")]
    [HasPermission(Permissions.ReportsExport)]
    [RequiresFeature(Features.ReportsExport)]
    public async Task<IActionResult> GenerateCsv(
        [FromBody] ReportFilterDTO filter)
    {
        var step = "Início";

        try
        {
            step = "Gerando dados do relatório";

            var report = await _reportDataService.GetReportAsync(
                filter,
                User.GetOrganizationId(),
                User.GetUserId(),
                User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty);

            step = "Gerando CSV";

            var csvBytes = _reportCsvService.Generate(report);

            step = "Montando nome do arquivo";

            var fileName = BuildFileName(
                report.SubjectName,
                report.StartDate,
                report.EndDate,
                "csv");

            return File(
                csvBytes,
                "text/csv; charset=utf-8",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Reports] Falha ao gerar CSV na etapa {Step}", step);

            return StatusCode(500, new
            {
                Step = step,
                Exception = ex.GetType().FullName,
                Message = ex.Message,
                InnerException = ex.InnerException?.Message,
                StackTrace = ex.StackTrace
            });
        }
    }

    private static string BuildFileName(
        string subjectName,
        DateTime startDate,
        DateTime endDate,
        string extension)
    {
        var cleanSubjectName = string.Concat(
            subjectName
                .Normalize(System.Text.NormalizationForm.FormD)
                .Where(character =>
                    System.Globalization.CharUnicodeInfo
                        .GetUnicodeCategory(character) !=
                    System.Globalization.UnicodeCategory.NonSpacingMark)
        );

        cleanSubjectName = new string(
            cleanSubjectName
                .Select(character =>
                    char.IsLetterOrDigit(character)
                        ? character
                        : '_')
                .ToArray());

        cleanSubjectName = cleanSubjectName.Trim('_');

        if (string.IsNullOrWhiteSpace(cleanSubjectName))
        {
            cleanSubjectName = "Relatorio";
        }

        return
            $"Relatorio_Produtividade_{cleanSubjectName}_" +
            $"{startDate:dd-MM-yyyy}_a_{endDate:dd-MM-yyyy}." +
            extension;
    }
}
