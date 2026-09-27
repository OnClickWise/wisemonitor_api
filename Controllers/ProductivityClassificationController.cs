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

        public ProductivityClassificationController(
            IProductivityClassificationService service)
        {
            _service = service;
        }

        [HttpGet("team/{teamId:guid}")]
        public async Task<ActionResult<IEnumerable<ProductivityClassificationResponseDTO>>> GetByTeam(
            Guid teamId)
        {
            var organizationId = User.GetOrganizationId();

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

            await _service.SaveAsync(
                organizationId,
                teamId,
                dto);

            return NoContent();
        }
    }
}
