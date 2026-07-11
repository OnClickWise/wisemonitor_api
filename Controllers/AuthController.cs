using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Services;
using Microsoft.IdentityModel.Tokens;
using WiseMonitor.Api.Helpers;
using System.IO;

namespace WiseMonitor.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly IEmailService _emailService;
        private readonly ILiveSessionService _liveSessionService;

        public AuthController(
            AppDbContext context,
            IJwtService jwtService,
            IEmailService emailService,
            ILiveSessionService liveSessionService)
        {
            _context = context;
            _jwtService = jwtService;
            _emailService = emailService;
            _liveSessionService = liveSessionService;
        }

        // ====================
        // LOGIN
        // ====================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDTO request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Email e senha são obrigatórios." });

            var user = await _context.Users
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                return Unauthorized(new { message = "Credenciais inválidas." });

            var sessionId = user.OrganizationId.HasValue
                ? await _liveSessionService.GetOrCreateSessionForOrganizationAsync(user.OrganizationId.Value)
                : Guid.NewGuid().ToString();

            var token = _jwtService.GenerateToken(user);

            var response = new
            {
                token,
                expiresIn = 3600,
                sessionId,
                organizationId = user.OrganizationId,
                user = new
                {
                    id = user.Id,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    role = user.Role
                }
            };

            return Ok(response);
        }

        // ====================
        // ESQUECI MINHA SENHA
        // ====================
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDTO request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "E-mail é obrigatório." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user != null)
            {
                var token = Guid.NewGuid().ToString("N");
                var resetToken = new PasswordResetToken
                {
                    UserId = user.Id,
                    Token = token,
                    Expiration = DateTime.UtcNow.AddHours(1)
                };

                _context.PasswordResetTokens.Add(resetToken);
                await _context.SaveChangesAsync();

                var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://wisemonitor.vercel.app";
                var resetLink = $"{frontendUrl}/reset-password?token={token}";

                try
                {
                    var templatePath = Path.Combine(AppContext.BaseDirectory, "html", "PasswordReset.html");
                    var htmlBody = await System.IO.File.ReadAllTextAsync(templatePath);

                    htmlBody = htmlBody.Replace("{{UserName}}", user.FirstName ?? "Usuário");
                    htmlBody = htmlBody.Replace("{{ResetLink}}", resetLink);

                    await _emailService.SendEmailAsync(
                        user.Email,
                        "Redefinição de Senha - WiseMonitor",
                        htmlBody
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AuthController] Falha ao enviar e-mail: {ex.Message}");
                }
            } 

            return Ok(new { message = "Se um usuário com este e-mail existir, um link de redefinição será enviado." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDTO request)
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { message = "Token e nova senha são obrigatórios." });

            var resetToken = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == request.Token && !t.Used);

            if (resetToken == null || resetToken.Expiration < DateTime.UtcNow)
                return BadRequest(new { message = "Token inválido ou expirado." });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == resetToken.UserId);
            
            if (user == null)
                return NotFound(new { message = "Usuário não encontrado." });

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            resetToken.Used = true;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Senha redefinida com sucesso." });
        }


// LOGOUT GLOBAL (ORGANIZAÇÃO)
// ====================
[Authorize]
[HttpPost("logout")]
public async Task<IActionResult> Logout()
{
    try
    {
        // Captura o token do header Authorization
        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return Unauthorized(new { message = "Token não fornecido ou inválido." });

        var token = authHeader.Substring("Bearer ".Length).Trim();

        // Extrai o userId do token JWT
        var userId = _jwtService.GetUserIdFromToken(token);
        if (userId == null)
            return Unauthorized(new { message = "Token inválido ou expirado." });

        // Busca o usuário no banco
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return Unauthorized(new { message = "Usuário não encontrado." });

        // Encerra a sessão global da organização (se aplicável)
        if (user.OrganizationId.HasValue)
            await _liveSessionService.EndSessionForOrganizationAsync(user.OrganizationId.Value);

        Console.WriteLine($"[AuthController] 🟡 Logout global realizado para organização {user.OrganizationId}");

        return Ok(new { message = "Logout realizado com sucesso para toda a organização." });
    }
    catch (SecurityTokenExpiredException)
    {
        return Unauthorized(new { message = "Token expirado. Faça login novamente." });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { message = $"Erro ao encerrar sessão: {ex.Message}" });
    }
}

    }
}
