using System.Globalization;
using System.Text;
using WiseMonitor.Api.DTOs.Reports;

namespace WiseMonitor.Api.Services.Reports;

public class ReportCsvService : IReportCsvService
{
    public byte[] Generate(ReportResponseDTO report)
    {
        var csv = new StringBuilder();

        // BOM UTF-8 para preservar acentos ao abrir no Excel.
        csv.Append('﻿');

        csv.AppendLine(
            "Aplicação;URL;Categoria;Início;Fim;Duração em segundos");

        foreach (var activity in report.Activities)
        {
            csv
                .Append(Escape(activity.ApplicationName))
                .Append(';')
                .Append(Escape(activity.Url))
                .Append(';')
                .Append(Escape(activity.Category))
                .Append(';')
                .Append(Escape(FormatDateTime(activity.StartTime)))
                .Append(';')
                .Append(Escape(FormatDateTime(activity.EndTime)))
                .Append(';')
                .Append(activity.DurationSeconds)
                .AppendLine();
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToString(
            "dd/MM/yyyy HH:mm:ss",
            CultureInfo.InvariantCulture);
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escapedValue = value.Replace("\"", "\"\"");

        if (
            escapedValue.Contains(';') ||
            escapedValue.Contains('"') ||
            escapedValue.Contains('\n') ||
            escapedValue.Contains('\r')
        )
        {
            return $"\"{escapedValue}\"";
        }

        return escapedValue;
    }
}
