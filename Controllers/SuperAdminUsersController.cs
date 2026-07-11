using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.DTOs.SuperAdmin;
using WiseMonitor.Api.Models.Enums;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [Route("api/super-admin/users")]
    public class SuperAdminUsersController : SuperAdminBaseController
    {
        private readonly ISuperAdminUserService _service;

        public SuperAdminUsersController(ISuperAdminUserService service)
            => _service = service;

        /// <summary>Lista todos os usuários da plataforma com filtros cross-tenant.</summary>
        [HttpGet]
        [HasPermission(Permissions.UsersView)]
        public async Task<IActionResult> GetAll([FromQuery] SuperAdminUserFilterDTO filter, CancellationToken ct)
            => OkResponse(await _service.GetAllAsync(filter, ct));

        /// <summary>Cria um novo Super Admin de plataforma.</summary>
        [HttpPost]
        [HasPermission(Permissions.UsersCreate)]
        public async Task<IActionResult> Create([FromBody] CreateSuperAdminDTO dto, CancellationToken ct)
        {
            try
            {
                var result = await _service.CreateSuperAdminAsync(dto, CurrentUserId, ct);
                return CreatedResponse(result, "Super Admin criado com sucesso");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        /// <summary>Obtém um usuário pelo ID.</summary>
        [HttpGet("{id:guid}")]
        [HasPermission(Permissions.UsersView)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        {
            var user = await _service.GetByIdAsync(id, ct);
            return user is null ? NotFoundResponse() : OkResponse(user);
        }

        /// <summary>Atualiza dados do usuário.</summary>
        [HttpPatch("{id:guid}")]
        [HasPermission(Permissions.UsersEdit)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UserUpdateDTO dto, CancellationToken ct)
        {
            try
            {
                await _service.UpdateAsync(id, dto, ct);
                return OkResponse<object>(null!, "Usuário atualizado");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }

        /// <summary>Exclui um usuário da plataforma.</summary>
        [HttpDelete("{id:guid}")]
        [HasPermission(Permissions.UsersDelete)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            try
            {
                await _service.DeleteAsync(id, CurrentUserId, ct);
                return OkResponse<object>(null!, "Usuário removido");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        /// <summary>Invalida todas as sessões ativas do usuário.</summary>
        [HttpPost("{id:guid}/force-logout")]
        [HasPermission(Permissions.UsersEdit)]
        public async Task<IActionResult> ForceLogout(Guid id, CancellationToken ct)
        {
            await _service.InvalidateAllSessionsAsync(id, ct);
            return OkResponse<object>(null!, "Todas as sessões invalidadas");
        }

        /// <summary>Desbloqueia um usuário inativo.</summary>
        [HttpPost("{id:guid}/unlock")]
        [HasPermission(Permissions.UsersEdit)]
        public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
        {
            try
            {
                await _service.UnlockAsync(id, CurrentUserId, ct);
                return OkResponse<object>(null!, "Usuário desbloqueado");
            }
            catch (KeyNotFoundException)
            {
                return NotFoundResponse();
            }
        }

        /// <summary>Retorna sessões ativas do usuário.</summary>
        [HttpGet("{id:guid}/sessions")]
        [HasPermission(Permissions.UsersView)]
        public async Task<IActionResult> GetSessions(Guid id, CancellationToken ct)
            => OkResponse(await _service.GetActiveSessionsAsync(id, ct));
    }
}
