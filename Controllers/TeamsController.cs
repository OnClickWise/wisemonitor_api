using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs.Team;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/teams")]
    [Authorize]
    public class TeamsController : ControllerBase
    {
        private readonly ITeamService _teamService;

        public TeamsController(ITeamService teamService)
        {
            _teamService = teamService;
        }

        // ======================================================
        // 📌 CREATE TEAM
        // ======================================================
        [HttpPost]
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

            var teams = await _teamService.GetAllAsync(organizationId);

            return Ok(teams);
        }

        // ======================================================
        // 📌 GET TEAM BY ID
        // ======================================================
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var organizationId = User.GetOrganizationId();

            var team = await _teamService.GetByIdAsync(id, organizationId);

            if (team == null)
                return NotFound("Team não encontrada");

            return Ok(team);
        }

        // ======================================================
        // 📌 UPDATE TEAM
        // ======================================================
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(
            Guid id,
            [FromBody] UpdateTeamDTO dto)
        {
            var organizationId = User.GetOrganizationId();

            await _teamService.UpdateAsync(id, dto, organizationId);

            return Ok(new { message = "Team atualizada com sucesso" });
        }

        // ======================================================
        // 📌 DELETE TEAM
        // ======================================================
        [HttpDelete("{id:guid}")]
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
        public async Task<IActionResult> AddMember(Guid id, Guid userId)
        {
            var organizationId = User.GetOrganizationId();

            await _teamService.AddMemberAsync(id, userId, organizationId);

            return Ok(new { message = "Membro adicionado à team" });
        }

        // ======================================================
        // 📌 REMOVE MEMBER
        // ======================================================
        [HttpDelete("{id:guid}/members/{userId:guid}")]
        public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
        {
            var organizationId = User.GetOrganizationId();

            await _teamService.RemoveMemberAsync(id, userId, organizationId);

            return Ok(new { message = "Membro removido da team" });
        }
    }
}
