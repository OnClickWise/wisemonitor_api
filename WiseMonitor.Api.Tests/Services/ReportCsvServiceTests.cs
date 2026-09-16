using System.Text;
using WiseMonitor.Api.DTOs.Reports;
using WiseMonitor.Api.Services.Reports;
using Xunit;

namespace WiseMonitor.Api.Tests.Services;

public class ReportCsvServiceTests
{
    [Fact]
    public void Generate_StartsWithUtf8Bom()
    {
        var service = new ReportCsvService();
        var report = new ReportResponseDTO();

        var bytes = service.Generate(report);

        var bom = Encoding.UTF8.GetPreamble();
        Assert.Equal(bom, bytes.Take(bom.Length));
    }

    [Fact]
    public void Generate_EscapesFieldsContainingSemicolonsOrQuotes()
    {
        var service = new ReportCsvService();
        var report = new ReportResponseDTO
        {
            Activities =
            {
                new ReportActivityDTO
                {
                    ApplicationName = "App; with \"quotes\"",
                    Url = "http://example.com",
                    Category = "Produtivo",
                    StartTime = new DateTime(2026, 8, 1, 10, 0, 0),
                    EndTime = new DateTime(2026, 8, 1, 10, 5, 0),
                    DurationSeconds = 300
                }
            }
        };

        var csv = Encoding.UTF8.GetString(service.Generate(report));

        Assert.Contains("\"App; with \"\"quotes\"\"\"", csv);
    }
}
