using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Services;
using WiseMonitor.Api.Extensions;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/keyboard")]
    public class KeyboardController : ControllerBase
    {
        private readonly IKeyboardService _service;

        public KeyboardController(IKeyboardService service)
        {
            _service = service;
        }

        // CREATE
        [HttpPost("events")]
        public async Task<IActionResult> Create(
            [FromBody] KeyboardEventCreateDTO dto)
        {
            var userId = User.GetUserId();
            var orgId = User.GetOrganizationId();

            // 🔧 CORREÇÃO:
            // O método não retorna valor (Task),
            // portanto não pode ser atribuído a uma variável
            await _service.ProcessKeyboardEventAsync(dto, userId, orgId);

            return Ok();
        }

        // READ BY ID
        [HttpGet("{id:guid}")]
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
        public async Task<IActionResult> Summary(
            [FromQuery] DateTime start,
            [FromQuery] DateTime end)
        {
            var userId = User.GetUserId();
            var result = await _service.GetSummaryAsync(userId, start, end);

            return Ok(result);
        }

        // UPDATE
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] KeyboardEventUpdateDTO dto)
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