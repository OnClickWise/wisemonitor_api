using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Repositories;
using WiseMonitor.Api.Services;
using System.Security.Claims;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Garante que apenas usuários autenticados podem acessar
    public class DevicesController : ControllerBase
    {
        private readonly IDeviceService _deviceService;
        private readonly ILiveMonitoringService _liveService;
        private readonly IUserService _userService;
        private readonly IScreenshotService _screenshotService;
        private readonly ITeamRepository _teamRepo;
        private readonly IAccessScopeService _scopeService;

        public DevicesController(
            IDeviceService deviceService,
            ILiveMonitoringService liveService,
            IUserService userService,
            IScreenshotService screenshotService,
            ITeamRepository teamRepo,
            IAccessScopeService scopeService)
        {
            _deviceService = deviceService;
            _liveService = liveService;
            _userService = userService;
            _screenshotService = screenshotService;
            _teamRepo = teamRepo;
            _scopeService = scopeService;
        }

        // Helper para pegar OrgId do token/claims
        private Guid GetOrgId()
        {
            var orgIdClaim = User.FindFirst("orgId")?.Value;
            return orgIdClaim != null ? Guid.Parse(orgIdClaim) : Guid.Empty;
        }

        // Supervisor só pode ver os devices dos usuários das equipes que administra —
        // retorna null quando o papel não precisa de restrição (vê a org toda).
        private async Task<HashSet<Guid>?> GetAllowedUserIdsAsync(Guid orgId)
        {
            var callerRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (UserRoles.Normalize(callerRole) != UserRoles.Supervisor)
                return null;

            var callerId = User.GetUserId();
            var memberIds = await _teamRepo.GetManagedMemberIdsAsync(callerId, orgId);
            return memberIds.ToHashSet();
        }

        // POST api/devices
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DeviceCreateDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var orgId = GetOrgId();
            if (orgId == Guid.Empty)
                return Forbid("Organização inválida.");

            var device = new Device
            {
                Id = Guid.NewGuid(),
                Hostname = dto.Hostname,
                IpAddress = dto.IpAddress,
                OrganizationId = orgId
            };

            var created = await _deviceService.CreateDeviceAsync(device, orgId);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }

        // GET api/devices
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var orgId = GetOrgId();
            var devices = await _deviceService.GetAllDevicesAsync(orgId);

            var allowedUserIds = await GetAllowedUserIdsAsync(orgId);
            if (allowedUserIds != null)
            {
                // Usuário efetivo: o atribuído manualmente tem prioridade sobre o
                // detectado pela última screenshot da máquina.
                var shotUsers = (await _screenshotService.GetAllByOrganizationAsync(orgId))
                    .Where(s => s.DeviceId != null)
                    .ToDictionary(s => s.DeviceId!, s => s.MonitoredUserId);

                devices = devices
                    .Where(d =>
                    {
                        var effective = d.UserId ??
                            (shotUsers.TryGetValue(d.Id.ToString(), out var u) ? u : (Guid?)null);
                        return effective.HasValue && allowedUserIds.Contains(effective.Value);
                    })
                    .ToList();
            }

            return Ok(devices);
        }

        // ============================================================
        // 🔴 DISPOSITIVOS ATIVOS (com dados do usuário)
        // ============================================================

        /// <summary>
        /// Retorna todos os dispositivos da organização com informações do
        /// usuário associado (via screenshot mais recente), usado pela tela
        /// "Dispositivos > Ao Vivo".
        /// </summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            var orgId = GetOrgId();
            if (orgId == Guid.Empty)
                return Forbid();

            var callerRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var allowedUserIds = await GetAllowedUserIdsAsync(orgId);

            var devices = await _deviceService.GetAllDevicesAsync(orgId);

            var latestScreenshots = (await _screenshotService.GetAllByOrganizationAsync(orgId))
                .ToList();

            var users = await _userService.GetAllUsersAsync(orgId, User.GetUserId(), callerRole);
            var userNames = users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
            var userEmails = users.ToDictionary(u => u.Id, u => u.Email);

            var screenshotByDevice = latestScreenshots
                .Where(s => s.DeviceId != null)
                .ToDictionary(s => s.DeviceId!, s => s.MonitoredUserId);

            var result = devices.Select(d =>
            {
                var hasScreenshot = screenshotByDevice.TryGetValue(d.Id.ToString(), out var monitoredUserId);

                // Usuário atribuído manualmente (correção de um admin) tem prioridade
                // sobre o detectado pela última screenshot.
                var effectiveUserId = d.UserId ?? (hasScreenshot ? monitoredUserId : (Guid?)null);
                var userName = effectiveUserId.HasValue && userNames.TryGetValue(effectiveUserId.Value, out var name)
                    ? name
                    : d.AgentEmail;
                var userEmail = effectiveUserId.HasValue && userEmails.TryGetValue(effectiveUserId.Value, out var email)
                    ? email
                    : d.AgentEmail;

                return new DeviceWithUserDto
                {
                    Id = d.Id,
                    Hostname = d.Hostname,
                    IpAddress = d.IpAddress,
                    IsOnline = d.IsOnline,
                    LastSeen = d.LastSeen,
                    UserName = userName,
                    UserEmail = userEmail,
                    UserId = effectiveUserId,
                };
            }).ToList();

            // Aplica o escopo de equipe do supervisor por último, sobre o UserId
            // já resolvido (manual ou via última screenshot).
            if (allowedUserIds != null)
            {
                result = result
                    .Where(d => d.UserId.HasValue && allowedUserIds.Contains(d.UserId.Value))
                    .ToList();
            }

            return Ok(result);
        }

        // GET api/devices/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var orgId = GetOrgId();

            if (!await _scopeService.CanAccessDeviceAsync(User, id.ToString()))
                return NotFound();

            var device = await _deviceService.GetDeviceByIdAsync(id, orgId);

            if (device == null)
                return NotFound();

            return Ok(device);
        }

        // ====================================================
        // PRESENÇA
        // ====================================================

        // POST api/devices/heartbeat
        // Chamado pelo agent enquanto a máquina está ligada.
        [HttpPost("heartbeat")]
        public async Task<IActionResult> Heartbeat(
            [FromBody] DeviceHeartbeatDTO dto, CancellationToken ct)
        {
            var orgId = GetOrgId();
            if (orgId == Guid.Empty)
                return Forbid();

            var device = await _deviceService.RegisterHeartbeatAsync(dto, orgId, ct);

            // Mantém o cache ao vivo em dia mesmo quando o monitoramento está parado
            // e nenhum screenshot/frame está chegando — senão o TTL expiraria o device
            // no painel com a máquina ligada.
            _liveService.RegisterOrUpdateDevice(new LiveDeviceUpdateDTO
            {
                DeviceId = device.Id.ToString(),
                OrgId = orgId.ToString(),
                Status = "online",
                Timestamp = device.LastSeen
            });

            return Ok(new DevicePresenceResultDTO
            {
                DeviceId = device.Id,
                IsOnline = device.IsOnline,
                LastSeen = device.LastSeen
            });
        }

        // POST api/devices/offline
        // Aviso de desligamento/logoff — o agent dispara antes de encerrar.
        [HttpPost("offline")]
        public async Task<IActionResult> Offline(
            [FromBody] DeviceOfflineDTO dto, CancellationToken ct)
        {
            var orgId = GetOrgId();
            if (orgId == Guid.Empty)
                return Forbid();

            var device = await _deviceService.MarkOfflineAsync(dto, orgId, ct);
            if (device == null)
                return NotFound();

            _liveService.MarkDeviceOffline(device.Id.ToString());

            return Ok(new DevicePresenceResultDTO
            {
                DeviceId = device.Id,
                IsOnline = false,
                LastSeen = device.LastSeen
            });
        }

        // PUT api/devices/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] DeviceUpdateDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var orgId = GetOrgId();

            // Supervisor só visualiza — reatribuir máquina mudaria quem entra no escopo dele.
            if (await GetAllowedUserIdsAsync(orgId) != null)
                return Forbid();

            var existing = await _deviceService.GetDeviceByIdAsync(id, orgId);
            if (existing == null)
                return NotFound();

            // Usuário precisa pertencer à mesma org — sem isso um admin poderia
            // atribuir um device a alguém de outra organização.
            string? resolvedUserName = null;
            if (dto.UserId.HasValue)
            {
                var assignedUser = await _userService.GetUserByIdAsync(dto.UserId.Value, orgId);
                if (assignedUser == null)
                    return BadRequest(new { message = "Usuário inválido para esta organização." });

                resolvedUserName = $"{assignedUser.FirstName} {assignedUser.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(resolvedUserName))
                    resolvedUserName = assignedUser.Email;
            }

            existing.Hostname = dto.Hostname;
            existing.IpAddress = dto.IpAddress;
            existing.UserId = dto.UserId;

            var updated = await _deviceService.UpdateDeviceAsync(existing, orgId);

            // Reflete a correção na tela ao vivo na hora, sem esperar o próximo
            // screenshot chegar e reprocessar o cache.
            if (resolvedUserName != null)
                _liveService.UpdateDeviceUser(id.ToString(), dto.UserId!.Value.ToString(), resolvedUserName);

            return Ok(updated);
        }

        // DELETE api/devices/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var orgId = GetOrgId();

            if (await GetAllowedUserIdsAsync(orgId) != null)
                return Forbid();

            var success = await _deviceService.DeleteDeviceAsync(id, orgId);

            if (!success)
                return NotFound();

            return NoContent();
        }
    }
}
