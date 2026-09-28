using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    // Sem PUT/DELETE de sessão: ninguém usava, e deixavam o próprio colaborador
    // reescrever as métricas de mouse dele.
    [ApiController]
    [Route("api/mouse")]
    [Authorize]
    public class MouseController : ControllerBase
    {
        private readonly IMouseService _service;
        private readonly IAccessScopeService _scopeService;

        public MouseController(
            IMouseService service,
            IAccessScopeService scopeService)
        {
            _service = service;
            _scopeService = scopeService;
        }

        // CREATE — enviado pelo agent, sempre em nome do usuário do token
        [HttpPost("events")]
        public async Task<IActionResult> Create(
            [FromBody] MouseEventCreateDTO dto)
        {
            var userId = User.GetUserId();
            var orgId = User.GetOrganizationId();
            if (userId == Guid.Empty || orgId == Guid.Empty)
                return Forbid();

            await _service.ProcessMouseEventAsync(dto, userId, orgId);

            return Ok();
        }

        // SESSÕES DA ORGANIZAÇÃO NO PERÍODO — uma chamada só para a tela de
        // Teclado & Mouse. Supervisor: só as equipes dele.
        [HttpGet("sessions")]
        [HasPermission(Permissions.ActivityView)]
        public async Task<IActionResult> GetSessions(
            [FromQuery] DateTime start,
            [FromQuery] DateTime? end,
            [FromQuery] Guid? userId)
        {
            var scope = await _scopeService.GetScopeAsync(User);
            if (scope.OrganizationId == Guid.Empty)
                return Forbid();

            if (start == default) start = DateTime.UtcNow;

            IReadOnlyCollection<Guid>? userIds = scope.IsRestricted ? scope.UserIds.ToList() : null;
            if (userId.HasValue)
            {
                if (!await _scopeService.CanAccessUserAsync(User, userId.Value))
                    return Forbid();
                userIds = new[] { userId.Value };
            }

            var result = await _service.GetByOrganizationAsync(
                scope.OrganizationId, start, end ?? start, userIds);

            return Ok(result);
        }

        // READ BY ID (sessão do próprio usuário)
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userId = User.GetUserId();
            var session = await _service.GetByIdAsync(id, userId);

            if (session == null)
                return NotFound();

            return Ok(session);
        }

        // READ HISTORY (do próprio usuário)
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] DateTime start,
            [FromQuery] DateTime end)
        {
            var userId = User.GetUserId();
            var result = await _service.GetHistoryAsync(userId, start, end);

            return Ok(result);
        }

        // SUMMARY (do próprio usuário)
        [HttpGet("summary")]
        public async Task<IActionResult> Summary(
            [FromQuery] DateTime start,
            [FromQuery] DateTime end)
        {
            var userId = User.GetUserId();
            var result = await _service.GetSummaryAsync(userId, start, end);

            return Ok(result);
        }

        // READ HISTORY - POR USUÁRIO (gestor consultando outro colaborador)
        [HttpGet("user/{userId:guid}")]
        public async Task<IActionResult> GetHistoryByUser(
            Guid userId,
            [FromQuery] DateTime start,
            [FromQuery] DateTime? end)
        {
            // 🔒 Usuário da mesma organização e dentro do escopo do caller
            // (supervisor: só membros das equipes que administra)
            if (!await _scopeService.CanAccessUserAsync(User, userId))
                return Forbid();

            if (start == default) start = DateTime.UtcNow;

            var result = await _service.GetHistoryAsync(userId, start, end ?? start);

            return Ok(result);
        }

        // SUMMARY - POR USUÁRIO
        [HttpGet("user/{userId:guid}/summary")]
        public async Task<IActionResult> GetSummaryByUser(
            Guid userId,
            [FromQuery] DateTime start,
            [FromQuery] DateTime? end)
        {
            if (!await _scopeService.CanAccessUserAsync(User, userId))
                return Forbid();

            if (start == default) start = DateTime.UtcNow;

            var result = await _service.GetSummaryAsync(userId, start, end ?? start);

            return Ok(result);
        }
    }
}
