using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WiseMonitor.Api.Authorization;
using WiseMonitor.Api.Utils;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/super-admin")]
    [Authorize]
    [SuperAdminOnly]
    public abstract class SuperAdminBaseController : ControllerBase
    {
        protected Guid CurrentUserId
        {
            get
            {
                var claim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
                return claim == null ? Guid.Empty : Guid.Parse(claim.Value);
            }
        }

        protected string CurrentUserEmail =>
            User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        protected IActionResult OkResponse<T>(T data, string? message = null) =>
            Ok(new ApiResponse<T> { Success = true, Data = data, Message = message ?? "Sucesso" });

        protected IActionResult CreatedResponse<T>(T data, string message = "Criado com sucesso") =>
            StatusCode(201, new ApiResponse<T> { Success = true, Data = data, Message = message });

        protected IActionResult NotFoundResponse(string message = "Recurso não encontrado") =>
            NotFound(new ApiResponse<object> { Success = false, Message = message });

        protected IActionResult BadRequestResponse(string message) =>
            BadRequest(new ApiResponse<object> { Success = false, Message = message });
    }
}
