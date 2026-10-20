using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Helpers;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers;

/// <summary>
/// Controller responsável pelo recebimento,
/// consulta e gerenciamento dos eventos de App Focus
/// (janela/aplicativo em uso).
/// </summary>
[ApiController]
[Route("api/monitoring/app-focus")]
[Authorize]
public class AppFocusController : ControllerBase
{
    private readonly IAppFocusService _service;
    private readonly IUserService _userService;
    private readonly IAccessScopeService _scopeService;
    private readonly ILogger<AppFocusController> _logger;

    public AppFocusController(
        IAppFocusService service,
        IUserService userService,
        IAccessScopeService scopeService,
        ILogger<AppFocusController> logger)
    {
        _service = service;
        _userService = userService;
        _scopeService = scopeService;
        _logger = logger;
    }

    // ============================================================
    // CREATE
    // ============================================================

    /// <summary>
    /// Registra um novo evento de foco de aplicativo
    /// enviado pelo agente Desktop
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Register(
        [FromBody] AppFocusEventCreateDTO dto)
    {
        var organizationId = User.GetOrganizationId();
        var userId = await ResolveMonitoredUserAsync(dto.UserId, organizationId);

        _logger.LogInformation(
            "📡 [API] AppFocus recebido | Org={OrgId} | User={UserId} | Device={DeviceId} | App={App}",
            organizationId,
            userId,
            dto.DeviceId,
            dto.ApplicationName
        );

        await _service.RegisterEventAsync(
            dto,
            userId,
            organizationId
        );

        _logger.LogInformation(
            "✅ [API] AppFocus processado com sucesso | Org={OrgId} | User={UserId}",
            organizationId,
            userId
        );

        return Ok();
    }

    // ============================================================
    // READ - LIST (por data / organização)
    // ============================================================

    /// <summary>
    /// Retorna todos os eventos de AppFocus
    /// de uma organização em uma data específica
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
    [FromQuery] DateTime startDate,
    [FromQuery] DateTime? endDate,
    [FromQuery] Guid? userId,
    [FromQuery] Guid? teamId,
    [FromQuery] DateTime? from,
    [FromQuery] DateTime? to)
    {
        var organizationId = User.GetOrganizationId();
        if (organizationId == Guid.Empty)
            return Forbid();

        // from/to: instantes exatos (o front manda o início e o fim do dia no fuso de
        // quem usa, já com o horário do filtro). startDate/endDate: dias inteiros em UTC,
        // mantidos por compatibilidade — no Brasil cortavam o dia às 21h.
        DateTime rangeStart, rangeEnd;
        if (from.HasValue && to.HasValue)
        {
            rangeStart = from.Value.ToUniversalTime();
            rangeEnd = to.Value.ToUniversalTime();
            if (rangeEnd <= rangeStart)
                return BadRequest(new { error = "Período inválido." });
            if (rangeEnd - rangeStart > TimeSpan.FromDays(93))
                return BadRequest(new { error = "Período máximo de 93 dias." });
        }
        else
        {
            if (startDate == default) startDate = DateTime.UtcNow;
            (rangeStart, rangeEnd) = ActivityPeriod.Days(startDate, endDate);
        }

        // Supervisor: só os usuários das equipes que administra — filtrado no banco.
        var scope = await _scopeService.GetScopeAsync(User);
        IReadOnlyCollection<Guid>? userIds = scope.IsRestricted ? scope.UserIds.ToList() : null;

        if (teamId.HasValue)
        {
            if (!scope.CanAccessTeam(teamId.Value))
                return Forbid();

            var teamUserIds = await _scopeService.GetTeamUserIdsAsync(teamId.Value, organizationId);
            userIds = userIds == null
                ? teamUserIds
                : teamUserIds.Where(userIds.Contains).ToList();
        }

        if (userId.HasValue)
        {
            if (!await _scopeService.CanAccessUserAsync(User, userId.Value))
                return Forbid();
            if (userIds != null && teamId.HasValue && !userIds.Contains(userId.Value))
                return Ok(Array.Empty<AppFocusEventResponseDTO>());
            userIds = new[] { userId.Value };
        }

        _logger.LogInformation(
            "📊 [API] Listando AppFocus | Org={OrgId} | Período={Start} até {End}",
            organizationId,
            rangeStart,
            rangeEnd
        );

        var events = await _service.GetAllAsync(
            organizationId,
            rangeStart,
            rangeEnd,
            userIds
        );

        return Ok(events);
    }

    // ============================================================
    // READ - BY ID
    // ============================================================

    /// <summary>
    /// Retorna um evento específico de AppFocus
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var organizationId = User.GetOrganizationId();

        var result = await _service
            .GetByIdAsync(id, organizationId);

        var scope = await _scopeService.GetScopeAsync(User);
        if (result == null || !scope.CanAccessUser(result.UserId))
        {
            _logger.LogWarning(
                "⚠️ [API] AppFocus não encontrado | EventId={EventId}",
                id
            );
            return NotFound();
        }

        return Ok(result);
    }

    // ============================================================
    // UPDATE
    // ============================================================

    /// <summary>
    /// Atualiza um evento de AppFocus existente
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] AppFocusEventUpdateDTO dto)
    {
        var organizationId = User.GetOrganizationId();

        if (!await CanAccessEventAsync(id, organizationId))
            return NotFound();

        await _service
            .UpdateAsync(id, dto, organizationId);

        return NoContent();
    }

    // ============================================================
    // HISTORICO DE ATIVIDADE
    // ============================================================

    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetHistory(
        Guid userId, 
        [FromQuery] DateTime start, 
        [FromQuery] DateTime? end) // 'end' é opcional (nullable)
    {
        // 🔒 Usuário da mesma organização e dentro do escopo do caller
        // (supervisor: só membros das equipes que administra)
        if (!await _scopeService.CanAccessUserAsync(User, userId))
            return Forbid();

        if (start == default) start = DateTime.UtcNow;

        var finalEnd = end ?? start;

        var result = await _service.GetHistoryAsync(userId, start, finalEnd);

        return Ok(result);
    }

    /// <summary>
    /// Atividades registradas por uma máquina, de qualquer usuário que a usou.
    /// </summary>
    [HttpGet("device/{deviceId}")]
    public async Task<IActionResult> GetDeviceHistory(
        string deviceId,
        [FromQuery] DateTime start,
        [FromQuery] DateTime end)
    {
        var organizationId = User.GetOrganizationId();
        if (organizationId == Guid.Empty)
            return Forbid();

        if (end < start)
            return BadRequest(new { message = "'end' deve ser posterior a 'start'." });

        // Mesma regra do vídeo/tela ao vivo: supervisor só vê máquinas das suas equipes.
        if (!await _scopeService.CanAccessDeviceAsync(User, deviceId))
            return NotFound();

        // Agents antigos identificavam a máquina pelo hostname; app-focus sempre usou Guid.
        if (!Guid.TryParse(deviceId, out var deviceGuid))
            return Ok(Array.Empty<object>());

        var result = await _service.GetDeviceHistoryAsync(organizationId, deviceGuid, start, end);

        return Ok(result);
    }

    // ============================================================
    // DELETE
    // ============================================================

    /// <summary>
    /// Remove um evento de AppFocus
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var organizationId = User.GetOrganizationId();

        if (!await CanAccessEventAsync(id, organizationId))
            return NotFound();

        await _service
            .DeleteAsync(id, organizationId);

        return NoContent();
    }

    /// <summary>
    /// Dono do evento: o usuário monitorado que o agent informa, se for da mesma
    /// organização. Antes era sempre o dono do token — e o token do agent é de quem
    /// fez login nele (o admin), então toda atividade da empresa caía no admin.
    /// Sem usuário informado, ou com um de fora da organização, vale o do token.
    /// </summary>
    private async Task<Guid> ResolveMonitoredUserAsync(Guid? reportedUserId, Guid organizationId)
    {
        var tokenUserId = User.GetUserId();

        if (reportedUserId is not { } monitored || monitored == Guid.Empty || monitored == tokenUserId)
            return tokenUserId;

        var user = await _userService.GetUserByIdAsync(monitored, organizationId);
        if (user != null)
            return monitored;

        _logger.LogWarning(
            "⚠️ [API] AppFocus com usuário fora da organização ignorado | Org={OrgId} | Informado={Reported} | Token={TokenUser}",
            organizationId, monitored, tokenUserId);
        return tokenUserId;
    }

    private async Task<bool> CanAccessEventAsync(Guid id, Guid organizationId)
    {
        var scope = await _scopeService.GetScopeAsync(User);
        if (!scope.IsRestricted)
            return true;

        var existing = await _service.GetByIdAsync(id, organizationId);
        return existing != null && scope.CanAccessUser(existing.UserId);
    }

    // ============================================================
    // METRICS
    // ============================================================

    /// <summary>
    /// Retorna métricas agregadas de uso de aplicativos
    /// por usuário e data
    /// </summary>
    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] DateTime date)
    {
        var userId = User.GetUserId();

        var metrics = await _service
            .GetMetricsAsync(userId, date);

        return Ok(metrics);
    }
}
