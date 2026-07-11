using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Extensions;
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
    private readonly ILogger<AppFocusController> _logger;

    public AppFocusController(
        IAppFocusService service,
        ILogger<AppFocusController> logger)
    {
        _service = service;
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
        var userId = User.GetUserId();
        var organizationId = User.GetOrganizationId();

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
    [FromQuery] DateTime endDate)
    {
        var organizationId = User.GetOrganizationId();

        _logger.LogInformation(
            "📊 [API] Listando AppFocus | Org={OrgId} | Período={Start} até {End}",
            organizationId,
            startDate,
            endDate
        );

        var events = await _service.GetAllAsync(
            organizationId,
            startDate,
            endDate
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

        if (result == null)
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
        
        if (start == default) start = DateTime.UtcNow;

        var finalEnd = end ?? start;

        var result = await _service.GetHistoryAsync(userId, start, finalEnd);

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

        await _service
            .DeleteAsync(id, organizationId);

        return NoContent();
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
