using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models.Billing;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/productivity-classifications")]
    [Authorize]
    public class ProductivityClassificationController : ControllerBase
    {
        private readonly IProductivityClassificationService _service;
        private readonly IAccessScopeService _scopeService;

        public ProductivityClassificationController(
            IProductivityClassificationService service,
            IAccessScopeService scopeService)
        {
            _service = service;
            _scopeService = scopeService;
        }

        [HttpGet("team/{teamId:guid}")]
        public async Task<ActionResult<IEnumerable<ProductivityClassificationResponseDTO>>> GetByTeam(
            Guid teamId)
        {
            var organizationId = User.GetOrganizationId();

            // Supervisor: só as equipes que administra.
            var scope = await _scopeService.GetScopeAsync(User);
            if (!scope.CanAccessTeam(teamId))
                return NotFound();

            var items = await _service.GetByTeamAsync(
                organizationId,
                teamId);

            return Ok(items);
        }

        [HttpPut("team/{teamId:guid}")]
        [RequiresFeature(Features.ProductivityAnalytics)]
        public async Task<IActionResult> Save(
            Guid teamId,
            [FromBody] ProductivityClassificationSaveDTO dto)
        {
            var organizationId = User.GetOrganizationId();

            var scope = await _scopeService.GetScopeAsync(User);
            if (!scope.CanAccessTeam(teamId))
                return NotFound();

            await _service.SaveAsync(
                organizationId,
                teamId,
                dto);

            return NoContent();
        }
    }
}
