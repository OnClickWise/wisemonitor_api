using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/application-display-names")]
    [Authorize]
    public class ApplicationDisplayNamesController : ControllerBase
    {
        private readonly IApplicationDisplayNameService _service;

        public ApplicationDisplayNamesController(
            IApplicationDisplayNameService service)
        {
            _service = service;
        }

        // ============================================================
        // GET
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var organizationId = User.GetOrganizationId();

            var items = await _service.GetAllAsync(
                organizationId);

            return Ok(items);
        }

        // ============================================================
        // CREATE / UPDATE
        // ============================================================

        [HttpPut]
        public async Task<IActionResult> Save(
            [FromBody] ApplicationDisplayNameUpdateDTO dto)
        {
            var organizationId = User.GetOrganizationId();

            var result = await _service.SaveAsync(
                organizationId,
                dto.ApplicationName,
                dto.DisplayName);

            return Ok(result);
        }
    }
}
