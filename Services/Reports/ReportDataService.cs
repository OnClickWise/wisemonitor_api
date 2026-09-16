using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs.Reports;
using WiseMonitor.Api.DTOs.Team;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Repositories;

namespace WiseMonitor.Api.Services.Reports;

public class ReportDataService : IReportDataService
{
    private readonly IAppFocusService _appFocusService;
    private readonly ITeamService _teamService;
    private readonly IUserService _userService;
    private readonly ITeamRepository _teamRepo;
    private readonly AppDbContext _context;
    private readonly ILogger<ReportDataService> _logger;

    public ReportDataService(
        IAppFocusService appFocusService,
        ITeamService teamService,
        IUserService userService,
        ITeamRepository teamRepo,
        AppDbContext context,
        ILogger<ReportDataService> logger)
    {
        _appFocusService = appFocusService;
        _teamService = teamService;
        _userService = userService;
        _teamRepo = teamRepo;
        _context = context;
        _logger = logger;
    }

    public async Task<ReportResponseDTO> GetReportAsync(
        ReportFilterDTO filter,
        Guid organizationId,
        Guid callerId,
        string callerRole)
    {
        ValidateFilter(filter);

        var isSupervisor = UserRoles.Normalize(callerRole) == UserRoles.Supervisor;

        // Escopo do supervisor: só pode gerar relatório da própria equipe —
        // sem isso, filtrando por um TeamId/UserId de fora, ou sem filtro
        // nenhum, ele enxergava a organização inteira.
        HashSet<Guid>? supervisorScopeIds = isSupervisor
            ? (await _teamRepo.GetManagedMemberIdsAsync(callerId, organizationId)).ToHashSet()
            : null;

        var organization = await _context.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                organization => organization.Id == organizationId);

        if (organization == null)
        {
            throw new KeyNotFoundException(
                "Organização não encontrada.");
        }

        var reportStart = DateTime.SpecifyKind(
            filter.StartDate.Date.Add(filter.StartTime),
            DateTimeKind.Local
        ).ToUniversalTime();

        var reportEnd = DateTime.SpecifyKind(
            filter.EndDate.Date.Add(filter.EndTime),
            DateTimeKind.Local
        ).ToUniversalTime();

        var subjectType = "organization";
        var subjectName = "Todos os colaboradores";

        TeamResponseDTO? selectedTeam = null;
        HashSet<Guid>? teamMemberIds = null;

        if (filter.TeamId.HasValue)
        {
            var teams = await _teamService.GetAllAsync(
                organizationId);

            selectedTeam = teams.FirstOrDefault(
                team => team.Id == filter.TeamId.Value);

            if (selectedTeam == null)
            {
                throw new KeyNotFoundException(
                    "Equipe não encontrada.");
            }

            if (
                isSupervisor &&
                selectedTeam.ManagerId != callerId &&
                !selectedTeam.Managers.Any(manager => manager.UserId == callerId)
            )
            {
                throw new UnauthorizedAccessException(
                    "Você não tem permissão para ver o relatório desta equipe.");
            }

            teamMemberIds = selectedTeam.Members
                .Select(member => member.UserId)
                .ToHashSet();

            subjectType = "team";
            subjectName = selectedTeam.Name;
        }

        if (filter.UserId.HasValue)
        {
            var selectedUser = await _userService.GetUserByIdAsync(
                filter.UserId.Value,
                organizationId);

            if (selectedUser == null)
            {
                throw new KeyNotFoundException(
                    "Usuário não encontrado.");
            }

            if (
                teamMemberIds != null &&
                !teamMemberIds.Contains(selectedUser.Id)
            )
            {
                throw new InvalidOperationException(
                    "O usuário selecionado não pertence à equipe informada.");
            }

            if (
                teamMemberIds == null &&
                supervisorScopeIds != null &&
                !supervisorScopeIds.Contains(selectedUser.Id)
            )
            {
                throw new UnauthorizedAccessException(
                    "Você não tem permissão para ver o relatório deste usuário.");
            }

            subjectType = "user";
            subjectName =
                $"{selectedUser.FirstName} {selectedUser.LastName}"
                .Trim();
        }

        IEnumerable<Guid> targetUserIds;

        if (filter.UserId.HasValue)
        {
            targetUserIds = new[] { filter.UserId.Value };
        }
        else if (teamMemberIds != null)
        {
            targetUserIds = teamMemberIds;
        }
        else
        {
            var organizationUsers =
                await _userService.GetAllUsersAsync(
                    organizationId,
                    callerId,
                    callerRole);

            targetUserIds = organizationUsers
                .Select(user => user.Id);
        }

        var distinctTargetUserIds = targetUserIds
            .Distinct()
            .ToList();

        var allEvents = new List<AppFocusEvent>();

        foreach (var userId in distinctTargetUserIds)
        {
            var userEvents =
                await _appFocusService.GetHistoryAsync(
                    userId,
                    reportStart,
                    reportEnd);

            allEvents.AddRange(userEvents);
        }

        allEvents = allEvents
            .OrderBy(appFocusEvent => appFocusEvent.StartTime)
            .ToList();

        _logger.LogInformation(
            "[Relatorio] Organizacao={OrganizationId} Usuarios={UserCount} Periodo={Start:O}..{End:O} Eventos={EventCount}",
            organizationId, distinctTargetUserIds.Count, reportStart, reportEnd, allEvents.Count);

        var filteredEvents = allEvents
            .Where(appFocusEvent =>
                appFocusEvent.OrganizationId == organizationId)
            .Where(appFocusEvent =>
                appFocusEvent.StartTime >= reportStart &&
                appFocusEvent.StartTime <= reportEnd)
            .ToList();

        var activities = filteredEvents
            .OrderBy(appFocusEvent => appFocusEvent.StartTime)
            .Select(appFocusEvent => new ReportActivityDTO
            {
                ApplicationName = appFocusEvent.ApplicationName,
                Url = appFocusEvent.Url,
                Category = FormatCategory(appFocusEvent.Category),
                StartTime = appFocusEvent.StartTime,
                EndTime =
                    appFocusEvent.EndTime ??
                    appFocusEvent.StartTime.AddSeconds(
                        Math.Max(0L, appFocusEvent.DurationSeconds)),
                DurationSeconds = Math.Max(0L, appFocusEvent.DurationSeconds)
            })
            .ToList();

        var productiveSeconds = filteredEvents
            .Where(appFocusEvent => appFocusEvent.Category == ActivityCategory.Productive)
            .Sum(appFocusEvent => Math.Max(0L, appFocusEvent.DurationSeconds));

        var neutralSeconds = filteredEvents
            .Where(appFocusEvent => appFocusEvent.Category == ActivityCategory.Neutral)
            .Sum(appFocusEvent => Math.Max(0L, appFocusEvent.DurationSeconds));

        var unproductiveSeconds = filteredEvents
            .Where(appFocusEvent => appFocusEvent.Category == ActivityCategory.Unproductive)
            .Sum(appFocusEvent => Math.Max(0L, appFocusEvent.DurationSeconds));

        var totalSeconds = productiveSeconds + neutralSeconds + unproductiveSeconds;

        var applicationsTotalSeconds = filteredEvents
            .Sum(appFocusEvent => Math.Max(0L, appFocusEvent.DurationSeconds));

        var topApplications = filteredEvents
            .GroupBy(appFocusEvent =>
                string.IsNullOrWhiteSpace(appFocusEvent.ApplicationName)
                    ? "Aplicação não identificada"
                    : appFocusEvent.ApplicationName.Trim())
            .Select(group =>
            {
                var applicationSeconds = group.Sum(appFocusEvent =>
                    Math.Max(0L, appFocusEvent.DurationSeconds));

                return new ReportApplicationDTO
                {
                    Name = group.Key,
                    TotalSeconds = applicationSeconds,
                    Percentage =
                        applicationsTotalSeconds > 0
                            ? applicationSeconds * 100d / applicationsTotalSeconds
                            : 0
                };
            })
            .OrderByDescending(application => application.TotalSeconds)
            .ThenBy(application => application.Name)
            .Take(5)
            .ToList();

        var dailyActivities = filteredEvents
            .GroupBy(appFocusEvent => appFocusEvent.StartTime.ToLocalTime().Date)
            .Select(group => new ReportDailyActivityDTO
            {
                Date = group.Key,
                ActivityCount = group.Count(),
                TotalSeconds = group.Sum(appFocusEvent => Math.Max(0L, appFocusEvent.DurationSeconds))
            })
            .OrderBy(day => day.Date)
            .ToList();

        return new ReportResponseDTO
        {
            CompanyName =
                !string.IsNullOrWhiteSpace(organization.BrandingDisplayName)
                    ? organization.BrandingDisplayName
                    : organization.Name,

            LogoUrl = organization.BrandingLogoUrl,

            SubjectType = subjectType,
            SubjectName = subjectName,

            StartDate = filter.StartDate.Date,
            EndDate = filter.EndDate.Date,

            StartTime = filter.StartTime,
            EndTime = filter.EndTime,

            ProductiveSeconds = productiveSeconds,
            NeutralSeconds = neutralSeconds,
            UnproductiveSeconds = unproductiveSeconds,

            // O sistema ainda não possui uma categoria de ociosidade.
            IdleSeconds = 0,

            TotalSeconds = totalSeconds,

            GeneratedAt = GetBrazilDateTime(),

            TopApplications = topApplications,

            DailyActivities = dailyActivities,

            Activities = activities
        };
    }

    private static void ValidateFilter(ReportFilterDTO filter)
    {
        if (filter.StartDate == default)
        {
            throw new ArgumentException("A data inicial é obrigatória.");
        }

        if (filter.EndDate == default)
        {
            throw new ArgumentException("A data final é obrigatória.");
        }

        if (filter.StartDate.Date > filter.EndDate.Date)
        {
            throw new ArgumentException("A data inicial não pode ser maior que a data final.");
        }

        var reportStart = DateTime.SpecifyKind(
            filter.StartDate.Date.Add(filter.StartTime),
            DateTimeKind.Local
        ).ToUniversalTime();

        var reportEnd = DateTime.SpecifyKind(
            filter.EndDate.Date.Add(filter.EndTime),
            DateTimeKind.Local
        ).ToUniversalTime();

        if (reportStart >= reportEnd)
        {
            throw new ArgumentException("O início do relatório deve ser anterior ao final.");
        }
    }

    private static DateTime GetBrazilDateTime()
    {
        var timeZoneId = OperatingSystem.IsWindows()
            ? "E. South America Standard Time"
            : "America/Sao_Paulo";

        var brazilTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, brazilTimeZone);
    }

    private static string FormatCategory(ActivityCategory category)
    {
        return category switch
        {
            ActivityCategory.Productive => "Produtivo",
            ActivityCategory.Neutral => "Neutro",
            ActivityCategory.Unproductive => "Improdutivo",
            _ => "Não classificado"
        };
    }
}
