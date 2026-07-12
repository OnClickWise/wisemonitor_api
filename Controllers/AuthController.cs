using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // ====================
        // LOGIN
        // ====================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Email e senha são obrigatórios." });

            try
            {
                var result = await _authService.LoginByEmailAsync(request.Email, request.Password);
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Credenciais inválidas." });
            }
        }

        // ====================
        // ESQUECI MINHA SENHA
        // ====================
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDTO request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "E-mail é obrigatório." });

            await _authService.RequestPasswordResetAsync(request.Email);

            return Ok(new { message = "Se um usuário com este e-mail existir, um link de redefinição será enviado." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDTO request)
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { message = "Token e nova senha são obrigatórios." });

            try
            {
                await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
                return Ok(new { message = "Senha redefinida com sucesso." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ====================
        // LOGOUT GLOBAL (ORGANIZAÇÃO)
        // ====================
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return Unauthorized(new { message = "Token não fornecido ou inválido." });

            try
            {
                await _authService.LogoutAsync(authHeader);
                return Ok(new { message = "Logout realizado com sucesso para toda a organização." });
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized(new { message = "Token inválido ou expirado." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Erro ao encerrar sessão: {ex.Message}" });
            }
        }
    }
}
