using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/live")]
    public class LiveController : ControllerBase
    {
        private readonly LiveStreamHub _hub;

        public LiveController(LiveStreamHub hub)
        {
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        }

        // ============================================================
        // 🏢 SNAPSHOT DA ORGANIZAÇÃO LOGADA
        // ============================================================
        [HttpGet("sessions/my-org")]
        [Authorize]
        public IActionResult GetSessionsForMyOrganization()
        {
            var orgClaim = User.FindFirst("organizationId")?.Value;

            if (!Guid.TryParse(orgClaim, out var organizationId))
                return Unauthorized("organizationId inválido no token.");

            var sessions = _hub.GetSessionsForOrg(organizationId);

            return Ok(new
            {
                organizationId,
                sessions
            });
        }

        // ============================================================
        // ❤️ HEALTH CHECK
        // ============================================================
        [HttpGet("health")]
        [AllowAnonymous]
        public IActionResult Health()
        {
            return Ok(new
            {
                status = "ok",
                service = "LiveStream",
                timestamp = DateTime.UtcNow
            });
        }
    }
}
