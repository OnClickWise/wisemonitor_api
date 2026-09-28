using System;
using System.Collections.Generic;
using System.Linq;
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
    // A organização vem sempre do token. O header "Organization-Id" que o front
    // ainda manda é ignorado: confiar nele deixava qualquer usuário logado ler e
    // alterar jornadas de outra organização.
    [ApiController]
    [Route("api/work-schedules")]
    [Authorize]
    public class WorkScheduleController : ControllerBase
    {
        private readonly IWorkScheduleService _service;
        private readonly IAccessScopeService _scopeService;

        public WorkScheduleController(IWorkScheduleService service, IAccessScopeService scopeService)
        {
            _service = service;
            _scopeService = scopeService;
        }

        private static bool CanSee(ScheduleAccess access) =>
            access is ScheduleAccess.Read or ScheduleAccess.Manage;

        [HttpPost]
        [HasPermission(Permissions.SchedulesManage)]
        public async Task<ActionResult<WorkScheduleResponseDTO>> Create([FromBody] WorkScheduleCreateDTO dto)
        {
            var organizationId = User.GetOrganizationId();
            if (organizationId == Guid.Empty)
                return Forbid();

            var result = await _service.CreateAsync(organizationId, dto, User.GetUserId());
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkScheduleResponseDTO>>> GetAll()
        {
            var organizationId = User.GetOrganizationId();
            if (organizationId == Guid.Empty)
                return Forbid();

            var visible = await _scopeService.GetVisibleSchedulesAsync(User);
            var all = await _service.GetAllAsync(organizationId);

            var result = all
                .Where(s => visible.ContainsKey(s.Id))
                .Select(s =>
                {
                    s.CanManage = visible[s.Id] == ScheduleAccess.Manage;
                    return s;
                })
                .ToList();

            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<WorkScheduleResponseDTO>> GetById(Guid id)
        {
            var access = await _scopeService.GetScheduleAccessAsync(User, id);
            if (!CanSee(access))
                return NotFound();

            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound();

            result.CanManage = access == ScheduleAccess.Manage;
            return Ok(result);
        }

        [HttpPut("{id:guid}")]
        [HasPermission(Permissions.SchedulesManage)]
        public async Task<ActionResult<WorkScheduleResponseDTO>> Update(
            Guid id,
            [FromBody] WorkScheduleUpdateDTO dto)
        {
            var access = await _scopeService.GetScheduleAccessAsync(User, id);
            if (!CanSee(access))
                return NotFound();

            // Supervisor: jornada usada também por outra equipe não pode mudar daqui.
            if (access != ScheduleAccess.Manage)
                return Forbid();

            var result = await _service.UpdateAsync(id, dto);
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        [HasPermission(Permissions.SchedulesManage)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var access = await _scopeService.GetScheduleAccessAsync(User, id);
            if (!CanSee(access))
                return NotFound();

            if (access != ScheduleAccess.Manage)
                return Forbid();

            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }

        [HttpPost("assign")]
        [HasPermission(Permissions.SchedulesManage)]
        public async Task<IActionResult> AssignToUser([FromBody] AssignUserScheduleDTO dto)
        {
            // Usuário da mesma org e, para o supervisor, de uma das equipes dele.
            if (!await _scopeService.CanAccessUserAsync(User, dto.UserId))
                return NotFound();

            var access = await _scopeService.GetScheduleAccessAsync(User, dto.WorkScheduleId);
            if (!CanSee(access))
                return NotFound();

            var success = await _service.AssignToUserAsync(dto, User.GetOrganizationId());
            if (!success)
                return BadRequest("Unable to assign work schedule.");

            return Ok();
        }

        [HttpPost("{id:guid}/clone")]
        [HasPermission(Permissions.SchedulesManage)]
        public async Task<ActionResult<WorkScheduleResponseDTO>> Clone(
            Guid id,
            [FromBody] CloneWorkScheduleDTO dto)
        {
            var access = await _scopeService.GetScheduleAccessAsync(User, id);
            if (!CanSee(access))
                return NotFound();

            try
            {
                var result = await _service.CloneAsync(id, dto.NewName, User.GetOrganizationId(), User.GetUserId());
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("{id:guid}/users")]
        public async Task<IActionResult> GetUsers(Guid id)
        {
            var access = await _scopeService.GetScheduleAccessAsync(User, id);
            if (!CanSee(access))
                return NotFound();

            var result = await _service.GetUsersAsync(id);

            // Supervisor: só os usuários das equipes que administra.
            var scope = await _scopeService.GetScopeAsync(User);
            if (scope.IsRestricted)
                result = result.Where(u => scope.CanAccessUser(u.UserId)).ToList();

            return Ok(result);
        }

        [HttpGet("users/{userId:guid}/history")]
        public async Task<IActionResult> GetUserHistory(Guid userId)
        {
            if (!await _scopeService.CanAccessUserAsync(User, userId))
                return NotFound();

            var result = await _service.GetUserHistoryAsync(userId, User.GetOrganizationId());
            return Ok(result);
        }

        [HttpGet("users/{userId:guid}/current")]
        public async Task<IActionResult> GetCurrentSchedule(Guid userId)
        {
            if (!await _scopeService.CanAccessUserAsync(User, userId))
                return NotFound();

            var result = await _service.GetCurrentScheduleAsync(userId, User.GetOrganizationId());
            if (result == null)
                return NotFound();

            return Ok(result);
        }
    }

    public class CloneWorkScheduleDTO
    {
        public string NewName { get; set; } = string.Empty;
    }
}
