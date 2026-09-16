using System.Globalization;
using System.Net;
using System.Text;
using WiseMonitor.Api.DTOs.Reports;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace WiseMonitor.Api.Services.Reports;

public class ReportPdfService : IReportPdfService
{
    private readonly IWebHostEnvironment _environment;

    public ReportPdfService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<byte[]> GenerateAsync(
    ReportResponseDTO report)
    {
        var step = "Início";

        try
        {
            step = "Localizando template";

            var templatePath = Path.Combine(
                _environment.ContentRootPath,
                "html",
                "Reports",
                "ProductivityReport.html");

            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException(
                    "Template não encontrado.",
                    templatePath);
            }

            step = "Lendo template";

            var template = await File.ReadAllTextAsync(templatePath);

            step = "Montando HTML";

            var html = BuildHtml(template, report);

            step = "Localizando Chromium";

            var chromiumExecutablePath =
                Environment.GetEnvironmentVariable(
                    "CHROMIUM_EXECUTABLE_PATH");

            if (string.IsNullOrWhiteSpace(chromiumExecutablePath))
            {
                step = "Baixando Chromium local";

                var browserFetcher = new BrowserFetcher();
                var installedBrowser = await browserFetcher.DownloadAsync();

                chromiumExecutablePath =
                    installedBrowser.GetExecutablePath();
            }

            if (!File.Exists(chromiumExecutablePath))
            {
                throw new FileNotFoundException(
                    "O executável do Chromium não foi encontrado.",
                    chromiumExecutablePath);
            }

            step = "Abrindo Chromium";

            await using var browser = await Puppeteer.LaunchAsync(
                new LaunchOptions
                {
                    Headless = true,
                    ExecutablePath = chromiumExecutablePath,

                    Args =
                    [
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-gpu",
                        "--no-zygote"
                    ]
                });

            step = "Criando página";

            await using var page = await browser.NewPageAsync();

            step = "Carregando HTML";

            await page.SetContentAsync(
                html,
                new NavigationOptions
                {
                    WaitUntil = [WaitUntilNavigation.Networkidle0]
                });

            step = "Gerando PDF";

            return await page.PdfDataAsync(
                new PdfOptions
                {
                    Format = PaperFormat.A4,
                    Landscape = true,
                    PrintBackground = true
                });
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"ERRO PDF | Etapa: {step} | {ex.GetType().Name}: {ex.Message}",
                ex);
        }
    }

    private static string BuildHtml(
        string template,
        ReportResponseDTO report)
    {
        var activitiesTable = BuildActivitiesTable(
            report.Activities);

        return template
            .Replace(
                "{{COMPANY_LOGO}}",
                string.Empty)
            .Replace(
                "{{COMPANY_NAME}}",
                Encode(report.CompanyName))
            .Replace(
                "{{GENERATED_AT}}",
                FormatDateTime(report.GeneratedAt))
            .Replace(
                "{{SUBJECT_TYPE}}",
                Encode(FormatSubjectType(report.SubjectType)))
            .Replace(
                "{{SUBJECT_NAME}}",
                Encode(report.SubjectName))
            .Replace(
                "{{REPORT_PERIOD}}",
                FormatPeriod(
                    report.StartDate,
                    report.EndDate))
            .Replace(
                "{{REPORT_TIME}}",
                FormatTimePeriod(
                    report.StartTime,
                    report.EndTime))
            .Replace(
                "{{PRODUCTIVE_TIME}}",
                FormatDuration(report.ProductiveSeconds))
            .Replace(
                "{{NEUTRAL_TIME}}",
                FormatDuration(report.NeutralSeconds))
            .Replace(
                "{{UNPRODUCTIVE_TIME}}",
                FormatDuration(report.UnproductiveSeconds))
            .Replace(
                "{{IDLE_TIME}}",
                FormatDuration(report.IdleSeconds))
            .Replace(
                "{{TOTAL_TIME}}",
                FormatDuration(report.TotalSeconds))
            .Replace(
                "{{PRODUCTIVITY_CHART}}",
                BuildProductivityChart(report))
            .Replace(
                "{{APPLICATIONS_CHART}}",
                BuildDailyActivitiesChart(
                    report.DailyActivities,
                    report.StartDate,
                    report.EndDate,
                    report.TotalSeconds))
            .Replace(
                "{{ACTIVITIES_TABLE}}",
                activitiesTable)
            .Replace(
                "{{PERIOD_ANALYSIS}}",
                BuildPeriodAnalysis(report))
            .Replace(
                "{{REPORT_CONCLUSION}}",
                BuildReportConclusion(report));
    }

    private static string BuildActivitiesTable(
        IReadOnlyCollection<ReportActivityDTO> activities)
    {
        if (activities.Count == 0)
        {
            return """
                <div class="empty-message">
                  Nenhuma atividade foi encontrada no período selecionado.
                </div>
                """;
        }

        var html = new StringBuilder();

        html.AppendLine("<table>");
        html.AppendLine("<thead>");
        html.AppendLine("<tr>");
        html.AppendLine("<th>Aplicação</th>");
        html.AppendLine("<th>URL</th>");
        html.AppendLine("<th>Categoria</th>");
        html.AppendLine("<th>Início</th>");
        html.AppendLine("<th>Fim</th>");
        html.AppendLine("<th>Duração</th>");
        html.AppendLine("</tr>");
        html.AppendLine("</thead>");
        html.AppendLine("<tbody>");

        foreach (var activity in activities)
        {
            html.AppendLine("<tr>");

            html
                .Append("<td>")
                .Append(Encode(activity.ApplicationName))
                .AppendLine("</td>");

            html
                .Append("<td>")
                .Append(Encode(activity.Url ?? "-"))
                .AppendLine("</td>");

            html
                .Append("<td>")
                .Append(Encode(activity.Category))
                .AppendLine("</td>");

            html
                .Append("<td>")
                .Append(FormatDateTime(activity.StartTime))
                .AppendLine("</td>");

            html
                .Append("<td>")
                .Append(FormatDateTime(activity.EndTime))
                .AppendLine("</td>");

            html
                .Append("<td>")
                .Append(FormatDuration(activity.DurationSeconds))
                .AppendLine("</td>");

            html.AppendLine("</tr>");
        }

        html.AppendLine("</tbody>");
        html.AppendLine("</table>");

        return html.ToString();
    }

    private static string BuildProductivityChart(
    ReportResponseDTO report)
    {
        var productiveSeconds =
            Math.Max(0L, report.ProductiveSeconds);

        var neutralSeconds =
            Math.Max(0L, report.NeutralSeconds);

        var unproductiveSeconds =
            Math.Max(0L, report.UnproductiveSeconds);

        var totalSeconds =
            productiveSeconds +
            neutralSeconds +
            unproductiveSeconds;

        if (totalSeconds <= 0)
        {
            return """
            <div class="empty-message">
              Não há dados suficientes para gerar o gráfico.
            </div>
            """;
        }

        var productivePercentage =
            productiveSeconds * 100d / totalSeconds;

        var neutralPercentage =
            neutralSeconds * 100d / totalSeconds;

        var unproductivePercentage =
            unproductiveSeconds * 100d / totalSeconds;

        var productiveEnd = productivePercentage;

        var neutralEnd =
            productivePercentage + neutralPercentage;

        var chartBackground =
            $"conic-gradient(" +
            $"#22c55e 0% {productiveEnd.ToString("0.##", CultureInfo.InvariantCulture)}%, " +
            $"#64748b {productiveEnd.ToString("0.##", CultureInfo.InvariantCulture)}% " +
            $"{neutralEnd.ToString("0.##", CultureInfo.InvariantCulture)}%, " +
            $"#ef4444 {neutralEnd.ToString("0.##", CultureInfo.InvariantCulture)}% 100%)";

        return $"""
        <div class="productivity-chart">
          <div
            class="productivity-donut"
            style="background: {chartBackground};"
          >
            <div class="productivity-donut-center">
              <strong>
                {FormatDuration(totalSeconds)}
              </strong>

              <span>
                Total
              </span>
            </div>
          </div>

          <div class="productivity-legend">
            {BuildProductivityLegendItem(
                    "Produtivo",
                    "#22c55e",
                    productiveSeconds,
                    productivePercentage)}

            {BuildProductivityLegendItem(
                    "Neutro",
                    "#64748b",
                    neutralSeconds,
                    neutralPercentage)}

            {BuildProductivityLegendItem(
                    "Improdutivo",
                    "#ef4444",
                    unproductiveSeconds,
                    unproductivePercentage)}
          </div>
        </div>
        """;
    }

    private static string BuildProductivityLegendItem(
    string label,
    string color,
    long seconds,
    double percentage)
    {
        return $"""
        <div class="productivity-legend-item">
          <span
            class="productivity-legend-color"
            style="background: {color};"
          ></span>

          <div class="productivity-legend-content">
            <span class="productivity-legend-label">
              {Encode(label)}
            </span>

            <strong>
              {percentage.ToString("0.0", CultureInfo.InvariantCulture)}%
            </strong>

            <small>
              {FormatDuration(seconds)}
            </small>
          </div>
        </div>
        """;
    }

    private static string BuildDailyActivitiesChart(
    IReadOnlyCollection<ReportDailyActivityDTO> dailyActivities,
    DateTime reportStartDate,
    DateTime reportEndDate,
    long reportTotalSeconds)
    {
        var activitiesByDate = dailyActivities
            .GroupBy(day => day.Date.Date)
            .ToDictionary(
                group => group.Key,
                group => new ReportDailyActivityDTO
                {
                    Date = group.Key,
                    ActivityCount = group.Sum(item => item.ActivityCount),
                    TotalSeconds = group.Sum(item => item.TotalSeconds)
                });

        var orderedDays = new List<ReportDailyActivityDTO>();

        for (
            var currentDate = reportStartDate.Date;
            currentDate <= reportEndDate.Date;
            currentDate = currentDate.AddDays(1)
        )
        {
            if (activitiesByDate.TryGetValue(
                currentDate,
                out var existingDay))
            {
                orderedDays.Add(existingDay);
            }
            else
            {
                orderedDays.Add(new ReportDailyActivityDTO
                {
                    Date = currentDate,
                    ActivityCount = 0,
                    TotalSeconds = 0
                });
            }
        }

        if (orderedDays.Count == 0)
        {
            return """
            <div class="empty-message">
              Não há dados suficientes para gerar o gráfico.
            </div>
            """;
        }

        var maxCount = Math.Max(
            1,
            orderedDays.Max(day => day.ActivityCount));

        var axisMaximum = CalculateChartAxisMaximum(maxCount);

        var axisStep = Math.Max(
            1,
            axisMaximum / 4);

        var daysWithActivities = orderedDays
            .Where(day => day.ActivityCount > 0)
            .ToList();

        ReportDailyActivityDTO? busiestDay = null;
        ReportDailyActivityDTO? quietestDay = null;

        if (daysWithActivities.Count > 0)
        {
            busiestDay = daysWithActivities
                .OrderByDescending(day => day.ActivityCount)
                .ThenBy(day => day.Date)
                .First();

            quietestDay = daysWithActivities
                .OrderBy(day => day.ActivityCount)
                .ThenBy(day => day.Date)
                .First();
        }

        var totalActivities = orderedDays.Sum(
            day => day.ActivityCount);

        var html = new StringBuilder();

        html.AppendLine(
            "<div class=\"daily-activity-chart\">");

        html.AppendLine(
            "<div class=\"daily-chart-main\">");

        // Eixo vertical
        html.AppendLine(
            "<div class=\"daily-y-axis\">");

        for (var axisValue = axisMaximum;
             axisValue >= 0;
             axisValue -= axisStep)
        {
            html
                .Append("<span>")
                .Append(axisValue)
                .AppendLine("</span>");
        }

        html.AppendLine("</div>");

        // Área do gráfico
        html.AppendLine(
            "<div class=\"daily-chart-area\">");

        html.AppendLine(
            "<div class=\"daily-grid-lines\">");

        for (var lineIndex = 0; lineIndex < 5; lineIndex++)
        {
            html.AppendLine("<span></span>");
        }

        html.AppendLine("</div>");

        html.AppendLine(
            "<div class=\"daily-bars\">");

        foreach (var day in orderedDays)
        {
            var heightPercentage =
                day.ActivityCount * 100d / axisMaximum;

            html.AppendLine(
                "<div class=\"daily-bar-column\">");

            html
                .Append("<div class=\"daily-bar-value\">")
                .Append(
                    day.ActivityCount > 0
                        ? day.ActivityCount.ToString()
                        : string.Empty)
                .AppendLine("</div>");

            html.AppendLine(
                "<div class=\"daily-bar-area\">");

            html
                .Append("<div class=\"daily-bar-fill\" style=\"height: ")
                .Append(
                    heightPercentage.ToString(
                        "0.##",
                        CultureInfo.InvariantCulture))
                .AppendLine("%;\"></div>");

            html.AppendLine("</div>");

            html
                .Append("<div class=\"daily-bar-label\">")
                .Append(day.Date.Day)
                .AppendLine("</div>");

            html.AppendLine("</div>");
        }

        html.AppendLine("</div>");
        html.AppendLine("</div>");
        html.AppendLine("</div>");

        // Resumo
        html.AppendLine(
            "<div class=\"daily-summary\">");

        html
            .Append("<div><strong>")
            .Append(totalActivities)
            .AppendLine("</strong><span>atividades</span></div>");

        html
            .Append("<div><strong>")
            .Append(FormatDuration(reportTotalSeconds))
            .AppendLine("</strong><span>tempo total</span></div>");

        html
            .Append("<div><strong>")
            .Append(
                busiestDay == null
                    ? "-"
                    : $"{busiestDay.Date:dd/MM} · {busiestDay.ActivityCount}")
            .AppendLine("</strong><span>dia com mais atividades</span></div>");

        html
            .Append("<div><strong>")
            .Append(
                quietestDay == null
                    ? "-"
                    : $"{quietestDay.Date:dd/MM} · {quietestDay.ActivityCount}")
            .AppendLine("</strong><span>dia com menos atividades</span></div>");

        html.AppendLine("</div>");
        html.AppendLine("</div>");

        return html.ToString();
    }

    private static int CalculateChartAxisMaximum(
    int maximumValue)
    {
        if (maximumValue <= 5)
        {
            return 5;
        }

        var magnitude = Math.Pow(
            10,
            Math.Floor(Math.Log10(maximumValue)));

        var normalizedValue =
            maximumValue / magnitude;

        double roundedNormalizedValue;

        if (normalizedValue <= 1)
        {
            roundedNormalizedValue = 1;
        }
        else if (normalizedValue <= 2)
        {
            roundedNormalizedValue = 2;
        }
        else if (normalizedValue <= 5)
        {
            roundedNormalizedValue = 5;
        }
        else
        {
            roundedNormalizedValue = 10;
        }

        return (int)(
            roundedNormalizedValue * magnitude);
    }

    private static string BuildPeriodAnalysis(
    ReportResponseDTO report)
    {
        var totalSeconds = Math.Max(0L, report.TotalSeconds);

        var totalActivities = report.Activities.Count;

        var productivePercentage =
            CalculatePercentage(
                report.ProductiveSeconds,
                totalSeconds);

        var neutralPercentage =
            CalculatePercentage(
                report.NeutralSeconds,
                totalSeconds);

        var unproductivePercentage =
            CalculatePercentage(
                report.UnproductiveSeconds,
                totalSeconds);

        var mostUsedApplication = report.TopApplications
            .OrderByDescending(application =>
                application.TotalSeconds)
            .FirstOrDefault();

        var firstParagraph = $"""
        <p>
          Durante o período analisado, foram registradas
          <strong>{FormatDurationLong(totalSeconds)}</strong>
          de atividade, distribuídas em
          <strong>{totalActivities}</strong>
          registros. Desse total,
          <strong>{FormatDurationLong(report.ProductiveSeconds)}</strong>
          foram classificados como produtivos
          ({productivePercentage.ToString("0.0", CultureInfo.InvariantCulture)}%),
          <strong>{FormatDurationLong(report.NeutralSeconds)}</strong>
          como neutros
          ({neutralPercentage.ToString("0.0", CultureInfo.InvariantCulture)}%)
          e
          <strong>{FormatDurationLong(report.UnproductiveSeconds)}</strong>
          como improdutivos
          ({unproductivePercentage.ToString("0.0", CultureInfo.InvariantCulture)}%).
        </p>
        """;

        if (mostUsedApplication == null)
        {
            return firstParagraph;
        }

        var secondParagraph = $"""
        <p>
          A aplicação com maior tempo de utilização foi
          <strong>{Encode(mostUsedApplication.Name)}</strong>,
          com
          <strong>{FormatDurationLong(mostUsedApplication.TotalSeconds)}</strong>
          registrados no período.
        </p>
        """;

        return firstParagraph + secondParagraph;
    }

    private static string BuildReportConclusion(
    ReportResponseDTO report)
    {
        var totalSeconds = Math.Max(0L, report.TotalSeconds);

        if (totalSeconds <= 0)
        {
            return """
            <p>
              Não foram encontrados dados suficientes para gerar uma conclusão
              sobre o período selecionado.
            </p>
            """;
        }

        var productivePercentage =
            CalculatePercentage(
                report.ProductiveSeconds,
                totalSeconds);

        var neutralPercentage =
            CalculatePercentage(
                report.NeutralSeconds,
                totalSeconds);

        var unproductivePercentage =
            CalculatePercentage(
                report.UnproductiveSeconds,
                totalSeconds);

        if (
            unproductivePercentage >= productivePercentage &&
            unproductivePercentage >= neutralPercentage
        )
        {
            return $"""
            <p>
              {Encode(GetSubjectLabel(report))}
              apresentou predominância de atividades improdutivas,
              representando
              <strong>{unproductivePercentage.ToString("0.0", CultureInfo.InvariantCulture)}%</strong>
              do período analisado. Recomenda-se avaliar as aplicações,
              os sites e os horários com maior concentração desses registros
              para compreender melhor a distribuição do tempo.
            </p>
            """;
        }

        if (
            productivePercentage >= neutralPercentage &&
            productivePercentage >= unproductivePercentage
        )
        {
            return $"""
            <p>
              {Encode(GetSubjectLabel(report))}
              apresentou predominância de atividades produtivas,
              correspondendo a
              <strong>{productivePercentage.ToString("0.0", CultureInfo.InvariantCulture)}%</strong>
              do período analisado. O resultado indica boa concentração
              em atividades classificadas como produtivas.
            </p>
            """;
        }

        return $"""
        <p>
          {Encode(GetSubjectLabel(report))}
          apresentou predominância de atividades neutras,
          representando
          <strong>{neutralPercentage.ToString("0.0", CultureInfo.InvariantCulture)}%</strong>
          do período analisado. Recomenda-se revisar as classificações
          e o contexto dessas atividades para uma interpretação mais precisa.
        </p>
        """;
    }

    private static double CalculatePercentage(
    long value,
    long total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return Math.Max(0L, value) * 100d / total;
    }

    private static string GetSubjectLabel(
    ReportResponseDTO report)
    {
        return report.SubjectType.ToLowerInvariant() switch
        {
            "user" => $"O usuário {report.SubjectName}",
            "team" => $"A equipe {report.SubjectName}",
            "organization" => "A organização",
            _ => report.SubjectName
        };
    }

    private static string FormatDurationLong(
    long totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        var duration = TimeSpan.FromSeconds(totalSeconds);

        var parts = new List<string>();

        var totalHours = (int)duration.TotalHours;

        if (totalHours > 0)
        {
            parts.Add($"{totalHours}h");
        }

        if (duration.Minutes > 0)
        {
            parts.Add($"{duration.Minutes}min");
        }

        if (duration.Seconds > 0 || parts.Count == 0)
        {
            parts.Add($"{duration.Seconds}s");
        }

        return string.Join(" ", parts);
    }

    private static string FormatSubjectType(string subjectType)
    {
        return subjectType.ToLowerInvariant() switch
        {
            "user" => "Usuário",
            "team" => "Equipe",
            "organization" => "Organização",
            _ => subjectType
        };
    }

    private static string FormatPeriod(
        DateTime startDate,
        DateTime endDate)
    {
        return
            $"{startDate:dd/MM/yyyy} até {endDate:dd/MM/yyyy}";
    }

    private static string FormatTimePeriod(
        TimeSpan startTime,
        TimeSpan endTime)
    {
        return
            $"{startTime:hh\\:mm} até {endTime:hh\\:mm}";
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToString(
            "dd/MM/yyyy HH:mm:ss",
            CultureInfo.InvariantCulture);
    }

    private static string FormatDuration(long totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        var duration = TimeSpan.FromSeconds(totalSeconds);

        var totalHours = (int)duration.TotalHours;

        return
            $"{totalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static string Encode(string? value)
    {
        return WebUtility.HtmlEncode(
            value ?? string.Empty);
    }
}
