using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.DTOs.Department;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Services;
using Perms = WiseMonitor.Api.Models.Enums.Permissions;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/departments")]
    [Authorize]
    public class DepartmentController : ControllerBase
    {
        private readonly IDepartmentService _service;
        private readonly IAuditService _audit;

        public DepartmentController(IDepartmentService service, IAuditService audit)
        {
            _service = service;
            _audit = audit;
        }

        [HttpGet]
        [HasPermission(Permissions.DepartmentsEdit)]
        public async Task<IActionResult> GetAll()
        {
            var orgId = User.GetOrganizationId();
            var result = await _service.GetAllAsync(orgId);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        [HasPermission(Permissions.DepartmentsEdit)]
        public async Task<IActionResult> GetById(Guid id)
        {
            var orgId = User.GetOrganizationId();
            var result = await _service.GetByIdAsync(id, orgId);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpGet("{id:guid}/overview")]
        [HasPermission(Permissions.DepartmentsEdit)]
        public async Task<IActionResult> GetOverview(Guid id)
        {
            var orgId = User.GetOrganizationId();
            try
            {
                var result = await _service.GetOverviewAsync(id, orgId);
                return Ok(result);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("{id:guid}/members")]
        [HasPermission(Permissions.DepartmentsEdit)]
        public async Task<IActionResult> GetMembers(Guid id)
        {
            var orgId = User.GetOrganizationId();
            try
            {
                var result = await _service.GetMembersAsync(id, orgId);
                return Ok(result);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("{id:guid}/supervisors")]
        [HasPermission(Permissions.DepartmentsEdit)]
        public async Task<IActionResult> GetSupervisors(Guid id)
        {
            var orgId = User.GetOrganizationId();
            try
            {
                var result = await _service.GetSupervisorsAsync(id, orgId);
                return Ok(result);
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost]
        [HasPermission(Permissions.DepartmentsCreate)]
        public async Task<IActionResult> Create([FromBody] CreateDepartmentDTO dto)
        {
            var orgId = User.GetOrganizationId();
            var result = await _service.CreateAsync(dto, orgId);

            await _audit.LogAsync(
                action: "create",
                entityType: "Department",
                entityId: result.Id.ToString(),
                details: $"Departamento '{result.Name}' criado",
                organizationId: orgId,
                userId: User.GetUserId(),
                userEmail: User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                userRole: User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:guid}")]
        [HasPermission(Permissions.DepartmentsEdit)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDepartmentDTO dto)
        {
            var orgId = User.GetOrganizationId();
            var result = await _service.UpdateAsync(id, dto, orgId);

            await _audit.LogAsync(
                action: "update",
                entityType: "Department",
                entityId: id.ToString(),
                organizationId: orgId,
                userId: User.GetUserId(),
                userEmail: User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                userRole: User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        [HasPermission(Permissions.DepartmentsDelete)]
        public async Task<IActionResult> Delete(Guid id)
        {
            var orgId = User.GetOrganizationId();
            await _service.DeleteAsync(id, orgId);

            await _audit.LogAsync(
                action: "delete",
                entityType: "Department",
                entityId: id.ToString(),
                organizationId: orgId,
                userId: User.GetUserId(),
                userEmail: User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                userRole: User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return NoContent();
        }

        [HttpPost("{id:guid}/members")]
        [HasPermission(Permissions.DepartmentsEdit)]
        public async Task<IActionResult> AddMember(Guid id, [FromBody] AddDepartmentMemberDTO dto)
        {
            var orgId = User.GetOrganizationId();
            try
            {
                await _service.AddMemberAsync(id, dto, orgId);

                await _audit.LogAsync(
                    action: "add_member",
                    entityType: "Department",
                    entityId: id.ToString(),
                    details: $"Membro {dto.UserId} adicionado com papel {dto.MemberRole}",
                    organizationId: orgId,
                    userId: User.GetUserId(),
                    userEmail: User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                    userRole: User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

                return Ok(new { message = "Membro adicionado ao departamento." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:guid}/members/{userId:guid}")]
        [HasPermission(Permissions.DepartmentsEdit)]
        public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
        {
            var orgId = User.GetOrganizationId();
            await _service.RemoveMemberAsync(id, userId, orgId);

            await _audit.LogAsync(
                action: "remove_member",
                entityType: "Department",
                entityId: id.ToString(),
                details: $"Membro {userId} removido",
                organizationId: orgId,
                userId: User.GetUserId(),
                userEmail: User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                userRole: User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { message = "Membro removido do departamento." });
        }

        [HttpPatch("{id:guid}/members/{userId:guid}/role")]
        [HasPermission(Permissions.DepartmentsEdit)]
        public async Task<IActionResult> UpdateMemberRole(Guid id, Guid userId, [FromBody] UpdateMemberRoleDTO dto)
        {
            var orgId = User.GetOrganizationId();
            try
            {
                await _service.UpdateMemberRoleAsync(id, userId, dto, orgId);

                await _audit.LogAsync(
                    action: "update_member_role",
                    entityType: "Department",
                    entityId: id.ToString(),
                    details: $"Papel do membro {userId} alterado para {dto.MemberRole}",
                    organizationId: orgId,
                    userId: User.GetUserId(),
                    userEmail: User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                    userRole: User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

                return Ok(new { message = "Papel atualizado com sucesso." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        [HttpPut("{id:guid}/director")]
        [HasPermission(Permissions.DepartmentsEdit)]
        public async Task<IActionResult> SetDirector(Guid id, [FromBody] SetDirectorDTO dto)
        {
            var orgId = User.GetOrganizationId();
            try
            {
                await _service.SetDirectorAsync(id, dto, orgId);

                await _audit.LogAsync(
                    action: "set_director",
                    entityType: "Department",
                    entityId: id.ToString(),
                    details: $"Diretor definido: {dto.UserId}",
                    organizationId: orgId,
                    userId: User.GetUserId(),
                    userEmail: User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                    userRole: User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

                return Ok(new { message = "Diretor definido com sucesso." });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }
    }
}
