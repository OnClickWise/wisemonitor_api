using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.DTOs.Team;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/teams")]
    [Authorize]
    public class TeamsController : ControllerBase
    {
        private readonly ITeamService _teamService;
        private readonly IAccessScopeService _scopeService;

        public TeamsController(ITeamService teamService, IAccessScopeService scopeService)
        {
            _teamService = teamService;
            _scopeService = scopeService;
        }

        // ======================================================
        // 📌 CREATE TEAM
        // ======================================================
        [HttpPost]
        [HasPermission(Permissions.TeamsCreate)]
        public async Task<IActionResult> Create([FromBody] CreateTeamDTO dto)
        {
            var organizationId = User.GetOrganizationId();

            await _teamService.CreateAsync(dto, organizationId);

            return Ok(new { message = "Team criada com sucesso" });
        }

        // ======================================================
        // 📌 GET ALL TEAMS (ORGANIZAÇÃO)
        // ======================================================
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var organizationId = User.GetOrganizationId();
            var scope = await _scopeService.GetScopeAsync(User);

            var teams = await _teamService.GetAllAsync(organizationId);

            // Supervisor vê só as equipes que administra.
            if (scope.IsRestricted)
                teams = teams.Where(t => scope.CanAccessTeam(t.Id)).ToList();

            return Ok(teams);
        }

        // ======================================================
        // 📌 GET TEAM BY ID
        // ======================================================
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var organizationId = User.GetOrganizationId();
            var scope = await _scopeService.GetScopeAsync(User);

            // 404 em vez de 403: não revela que a equipe existe.
            if (!scope.CanAccessTeam(id))
                return NotFound("Team não encontrada");

            var team = await _teamService.GetByIdAsync(id, organizationId);

            if (team == null)
                return NotFound("Team não encontrada");

            return Ok(team);
        }

        // ======================================================
        // 📌 UPDATE TEAM
        // ======================================================
        [HttpPut("{id:guid}")]
        [HasPermission(Permissions.TeamsEdit)]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateTeamDTO dto)
        {
            var organizationId = User.GetOrganizationId();
            var scope = await _scopeService.GetScopeAsync(User);

            if (!scope.IsRestricted)
            {
                await _teamService.UpdateAsync(id, dto, organizationId);
                return Ok(new { message = "Team atualizada com sucesso" });
            }

            // Supervisor: só nas próprias equipes, e só a jornada. Nome, membros e
            // administradores ficam com a gestão — se pudesse incluir membros, ele
            // ganharia acesso aos dados de quem quisesse da organização.
            if (!scope.CanAccessTeam(id))
                return NotFound("Team não encontrada");

            if (dto.WorkScheduleId.HasValue)
            {
                var access = await _scopeService.GetScheduleAccessAsync(User, dto.WorkScheduleId.Value);
                if (access is ScheduleAccess.NotFound or ScheduleAccess.Hidden)
                    return BadRequest(new { message = "Jornada inválida." });
            }

            await _teamService.UpdateWorkScheduleAsync(id, dto.WorkScheduleId, organizationId);

            return Ok(new { message = "Team atualizada com sucesso" });
        }

        // ======================================================
        // 📌 DELETE TEAM
        // ======================================================
        [HttpDelete("{id:guid}")]
        [HasPermission(Permissions.TeamsDelete)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var organizationId = User.GetOrganizationId();

            await _teamService.DeleteAsync(id, organizationId);

            return Ok(new { message = "Team removida com sucesso" });
        }

        // ======================================================
        // 📌 ADD MEMBER
        // ======================================================
        [HttpPost("{id:guid}/members/{userId:guid}")]
        [HasPermission(Permissions.TeamsEdit)]
        public async Task<IActionResult> AddMember(Guid id, Guid userId)
        {
            var scope = await _scopeService.GetScopeAsync(User);
            if (scope.IsRestricted)
                return Forbid();

            var organizationId = User.GetOrganizationId();

            await _teamService.AddMemberAsync(id, userId, organizationId);

            return Ok(new { message = "Membro adicionado à team" });
        }

        // ======================================================
        // 📌 REMOVE MEMBER
        // ======================================================
        [HttpDelete("{id:guid}/members/{userId:guid}")]
        [HasPermission(Permissions.TeamsEdit)]
        public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
        {
            var scope = await _scopeService.GetScopeAsync(User);
            if (scope.IsRestricted)
                return Forbid();

            var organizationId = User.GetOrganizationId();

            await _teamService.RemoveMemberAsync(id, userId, organizationId);

            return Ok(new { message = "Membro removido da team" });
        }
    }
}
