using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Extensions;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Services;
using System.IO;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IAuditService _auditService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<UserController> _logger;

        public UserController(
            IUserService userService,
            IAuditService auditService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<UserController> logger)
        {
            _userService = userService;
            _auditService = auditService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        private Guid GetOrganizationId()
        {
            var orgIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("orgId")?.Value;
            if (string.IsNullOrEmpty(orgIdClaim) || !Guid.TryParse(orgIdClaim, out var orgId))
                throw new UnauthorizedAccessException("Organização não encontrada ou ID inválido.");
            return orgId;
        }

        [HttpPost]
        [HasPermission(Permissions.UsersCreate)]
        public async Task<IActionResult> CreateUser([FromBody] UserCreateDTO dto)
        {
            try
            {
                var orgId = GetOrganizationId();
                // Normaliza o papel para o valor canônico
                dto.Role = UserRoles.Normalize(dto.Role);

                var createdUser = await _userService.CreateUserAsync(dto, orgId);

                await _auditService.LogAsync(
                    action: "create",
                    entityType: "User",
                    entityId: createdUser.Id.ToString(),
                    details: $"Usuário '{createdUser.Email}' criado com papel {createdUser.Role}",
                    organizationId: orgId,
                    userId: User.GetUserId(),
                    userEmail: User.FindFirst(ClaimTypes.Email)?.Value,
                    userRole: User.FindFirst(ClaimTypes.Role)?.Value,
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

                return CreatedAtAction(nameof(GetAllUsers), new { id = createdUser.Id }, createdUser);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar usuário");
                return BadRequest(new { error = "Erro ao criar usuário." });
            }
        }

        [HttpGet]
        [HasPermission(Permissions.UsersView)]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var orgId = GetOrganizationId();
                var users = await _userService.GetAllUsersAsync(orgId);
                return Ok(users);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao listar usuários");
                return StatusCode(500, new { error = "Erro interno ao listar usuários." });
            }
        }

        [HttpGet("{id:guid}")]
        [HasPermission(Permissions.UsersView)]
        public async Task<IActionResult> GetUserById(Guid id)
        {
            try
            {
                var orgId = GetOrganizationId();
                var user = await _userService.GetUserByIdAsync(id, orgId);
                return user == null ? NotFound() : Ok(user);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
        }

        [HttpPut("{id:guid}")]
        [HasPermission(Permissions.UsersEdit)]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UserUpdateDTO dto)
        {
            try
            {
                var orgId = GetOrganizationId();
                if (!string.IsNullOrWhiteSpace(dto.Role))
                    dto.Role = UserRoles.Normalize(dto.Role);

                var updatedUser = await _userService.UpdateUserAsync(id, dto, orgId);

                await _auditService.LogAsync(
                    action: "update",
                    entityType: "User",
                    entityId: id.ToString(),
                    organizationId: orgId,
                    userId: User.GetUserId(),
                    userEmail: User.FindFirst(ClaimTypes.Email)?.Value,
                    userRole: User.FindFirst(ClaimTypes.Role)?.Value,
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

                return Ok(updatedUser);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar usuário");
                return BadRequest(new { error = "Erro ao atualizar usuário." });
            }
        }

        [HttpDelete("{id:guid}")]
        [HasPermission(Permissions.UsersDelete)]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            try
            {
                var orgId = GetOrganizationId();
                bool deleted = await _userService.DeleteUserAsync(id, orgId);

                if (!deleted)
                    return NotFound(new { error = "Usuário não encontrado ou não pertence à organização." });

                await _auditService.LogAsync(
                    action: "delete",
                    entityType: "User",
                    entityId: id.ToString(),
                    organizationId: orgId,
                    userId: User.GetUserId(),
                    userEmail: User.FindFirst(ClaimTypes.Email)?.Value,
                    userRole: User.FindFirst(ClaimTypes.Role)?.Value,
                    ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao deletar usuário");
                return BadRequest(new { error = "Erro ao deletar usuário." });
            }
        }

        [HttpPut("me/avatar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateMyAvatar(
        [FromForm] UpdateMyAvatarDTO dto,
        IFormFile? avatarFile,
        CancellationToken ct
    )
            {
                try
                {
                    var userId = User.GetUserId();

                    string? avatarUrl = dto.AvatarUrl;

                    if (avatarFile != null && avatarFile.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "wwwroot",
                            "uploads",
                            "profile-photos"
                        );

                        if (!Directory.Exists(uploadsFolder))
                            Directory.CreateDirectory(uploadsFolder);

                        var extension = Path.GetExtension(avatarFile.FileName);
                        var fileName = $"{Guid.NewGuid()}{extension}";
                        var filePath = Path.Combine(uploadsFolder, fileName);

                        await using var stream = new FileStream(filePath, FileMode.Create);
                        await avatarFile.CopyToAsync(stream, ct);

                        avatarUrl = $"/uploads/profile-photos/{fileName}";
                    }

                    var user = await _userService.UpdateMyAvatarAsync(userId, avatarUrl);

                    return Ok(user);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao atualizar avatar do usuário");

                    return BadRequest(new
                    {
                        error = "Erro ao atualizar foto de perfil."
                    });
                }
            }

        // Retorna os papéis disponíveis para seleção
        [HttpGet("roles")]
        public IActionResult GetAvailableRoles()
        {
            var callerRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var normalized = UserRoles.Normalize(callerRole);

            // SuperAdmin e TenantAdmin veem todos os papéis de tenant
            var roles = normalized == UserRoles.SuperAdmin
                ? UserRoles.All
                : UserRoles.TenantLevel;

            return Ok(roles);
        }
    }
}
