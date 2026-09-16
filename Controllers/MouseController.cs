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
    [ApiController]
    [Route("api/mouse")]
    [Authorize]
    public class MouseController : ControllerBase
    {
        private readonly IMouseService _service;

        public MouseController(IMouseService service)
        {
            _service = service;
        }

        // CREATE
        [HttpPost("events")]
        public async Task<IActionResult> Create(
            [FromBody] MouseEventCreateDTO dto)
        {
            var userId = User.GetUserId();
            var orgId = User.GetOrganizationId();

            await _service.ProcessMouseEventAsync(dto, userId, orgId);

            return Ok();
        }

        // READ BY ID
        [HttpGet("{id:guid}")]
        [HasPermission(Permissions.ActivityView)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var userId = User.GetUserId();
            var session = await _service.GetByIdAsync(id, userId);

            if (session == null)
                return NotFound();

            return Ok(session);
        }

        // READ HISTORY
        [HttpGet("history")]
        [HasPermission(Permissions.ActivityView)]
        public async Task<IActionResult> GetHistory(
            [FromQuery] DateTime start,
            [FromQuery] DateTime end)
        {
            var userId = User.GetUserId();
            var result = await _service.GetHistoryAsync(userId, start, end);

            return Ok(result);
        }

        // SUMMARY
        [HttpGet("summary")]
        [HasPermission(Permissions.ActivityView)]
        public async Task<IActionResult> Summary(
            [FromQuery] DateTime start,
            [FromQuery] DateTime end)
        {
            var userId = User.GetUserId();
            var result = await _service.GetSummaryAsync(userId, start, end);

            return Ok(result);
        }

        // READ HISTORY DE OUTRO USUÁRIO (visão do admin/gestor sobre um colaborador)
        [HttpGet("user/{userId:guid}")]
        [HasPermission(Permissions.ActivityView)]
        public async Task<IActionResult> GetHistoryByUser(
            Guid userId,
            [FromQuery] DateTime start,
            [FromQuery] DateTime? end)
        {
            var result = await _service.GetHistoryAsync(userId, start, end ?? DateTime.UtcNow);

            return Ok(result);
        }

        // UPDATE
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] MouseEventUpdateDTO dto)
        {
            var userId = User.GetUserId();
            var updated = await _service.UpdateAsync(id, dto, userId);

            if (!updated)
                return NotFound();

            return NoContent();
        }

        // DELETE
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userId = User.GetUserId();
            var deleted = await _service.DeleteAsync(id, userId);

            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}
