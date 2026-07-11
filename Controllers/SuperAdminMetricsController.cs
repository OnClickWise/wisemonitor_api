using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [Route("api/super-admin/metrics")]
    public class SuperAdminMetricsController : SuperAdminBaseController
    {
        private readonly ISuperAdminMetricsService _service;

        public SuperAdminMetricsController(ISuperAdminMetricsService service)
            => _service = service;

        /// <summary>Visão geral da plataforma: tenants, usuários, dispositivos, saúde.</summary>
        [HttpGet]
        [HasPermission(Permissions.MetricsGlobal)]
        public async Task<IActionResult> GetOverview(CancellationToken ct)
            => OkResponse(await _service.GetOverviewAsync(ct));

        /// <summary>Série temporal de uma métrica (NewTenants, ActiveUsers, Screenshots, etc.).</summary>
        [HttpGet("timeseries")]
        [HasPermission(Permissions.MetricsGlobal)]
        public async Task<IActionResult> GetTimeseries([FromQuery] TimeseriesQueryDTO query, CancellationToken ct)
            => OkResponse(await _service.GetTimeseriesAsync(query, ct));

        /// <summary>Distribuição de tenants por plano.</summary>
        [HttpGet("plans")]
        [HasPermission(Permissions.MetricsGlobal)]
        public async Task<IActionResult> GetPlanDistribution(CancellationToken ct)
            => OkResponse(await _service.GetPlanDistributionAsync(ct));

        /// <summary>Status de saúde do sistema (DB, migrações, uptime).</summary>
        [HttpGet("/api/super-admin/system/health")]
        [HasPermission(Permissions.MetricsGlobal)]
        public async Task<IActionResult> GetSystemHealth(CancellationToken ct)
            => OkResponse(await _service.GetSystemHealthAsync(ct));
    }
}
