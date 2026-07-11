using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/work-schedules")]
    [Authorize]
    public class WorkScheduleController : ControllerBase
    {
        private readonly IWorkScheduleService _service;

        public WorkScheduleController(IWorkScheduleService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<ActionResult<WorkScheduleResponseDTO>> Create(
            [FromHeader(Name = "Organization-Id")] Guid organizationId,
            [FromBody] WorkScheduleCreateDTO dto)
        {
            if (organizationId == Guid.Empty)
                return BadRequest("Organization-Id header is required.");

            var result = await _service.CreateAsync(organizationId, dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<WorkScheduleResponseDTO>>> GetAll(
            [FromHeader(Name = "Organization-Id")] Guid organizationId)
        {
            if (organizationId == Guid.Empty)
                return BadRequest("Organization-Id header is required.");

            var result = await _service.GetAllAsync(organizationId);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<WorkScheduleResponseDTO>> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<WorkScheduleResponseDTO>> Update(
            Guid id,
            [FromBody] WorkScheduleUpdateDTO dto)
        {
            var result = await _service.UpdateAsync(id, dto);
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var deleted = await _service.DeleteAsync(id);
            if (!deleted)
                return NotFound();

            return NoContent();
        }

        [HttpPost("assign")]
        public async Task<IActionResult> AssignToUser([FromBody] AssignUserScheduleDTO dto)
        {
            var success = await _service.AssignToUserAsync(dto);
            if (!success)
                return BadRequest("Unable to assign work schedule.");

            return Ok();
        }

        [HttpPost("{id:guid}/clone")]
        public async Task<ActionResult<WorkScheduleResponseDTO>> Clone(
            Guid id,
            [FromBody] CloneWorkScheduleDTO dto,
            [FromHeader(Name = "Organization-Id")] Guid organizationId)
        {
            if (organizationId == Guid.Empty)
                return BadRequest("Organization-Id header is required.");

            try
            {
                var result = await _service.CloneAsync(id, dto.NewName, organizationId);
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
            var result = await _service.GetUsersAsync(id);
            return Ok(result);
        }

        [HttpGet("users/{userId:guid}/history")]
        public async Task<IActionResult> GetUserHistory(
            Guid userId,
            [FromHeader(Name = "Organization-Id")] Guid organizationId)
        {
            if (organizationId == Guid.Empty)
                return BadRequest("Organization-Id header is required.");

            var result = await _service.GetUserHistoryAsync(userId, organizationId);
            return Ok(result);
        }

        [HttpGet("users/{userId:guid}/current")]
        public async Task<IActionResult> GetCurrentSchedule(
            Guid userId,
            [FromHeader(Name = "Organization-Id")] Guid organizationId)
        {
            if (organizationId == Guid.Empty)
                return BadRequest("Organization-Id header is required.");

            var result = await _service.GetCurrentScheduleAsync(userId, organizationId);
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
