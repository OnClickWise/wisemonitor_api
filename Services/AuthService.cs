using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Helpers;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IJwtService _jwtService;
        private readonly ILiveSessionService _liveSessionService;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            AppDbContext context,
            IConfiguration configuration,
            IJwtService jwtService,
            ILiveSessionService liveSessionService,
            IEmailService emailService,
            ILogger<AuthService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _jwtService = jwtService ?? throw new ArgumentNullException(nameof(jwtService));
            _liveSessionService = liveSessionService ?? throw new ArgumentNullException(nameof(liveSessionService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<LoginResponseDTO> LoginAsync(LoginRequestDTO loginDto)
        {
            if (loginDto == null)
                throw new ArgumentNullException(nameof(loginDto));

            var user = await _context.Users
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(u =>
                    u.Email == loginDto.Email &&
                    u.OrganizationId == loginDto.OrganizationId &&
                    u.IsActive);

            if (user == null || !VerifyPassword(loginDto.Password, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Credenciais inválidas");
            }

            var token = GenerateJwtToken(user);
            return new LoginResponseDTO
            {
                Token = token,  
                UserId = user.Id,
                FullName = $"{(user.FirstName ?? "").Trim()} {(user.LastName ?? "").Trim()}".Trim(),
                Role = user.Role,
                OrganizationId = user.OrganizationId
            };

        }

        public string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be null or empty.", nameof(password));

            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool VerifyPassword(string? password, string? hashedPassword)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hashedPassword))
                return false;

            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];
            var issuer = jwtSettings["Issuer"];
            var audience = jwtSettings["Audience"];

            if (string.IsNullOrWhiteSpace(secretKey))
                throw new InvalidOperationException("JWT SecretKey is not configured.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.Role, user.Role ?? "User"),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            };

            if (user.OrganizationId.HasValue)
                claims.Add(new Claim("orgId", user.OrganizationId.Value.ToString()));

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(6),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<AuthLoginResultDTO> LoginByEmailAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Email e senha são obrigatórios.");

            var user = await _context.Users
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null || !VerifyPassword(password, user.PasswordHash))
                throw new UnauthorizedAccessException("Credenciais inválidas.");

            var sessionId = user.OrganizationId.HasValue
                ? await _liveSessionService.GetOrCreateSessionForOrganizationAsync(user.OrganizationId.Value)
                : Guid.NewGuid().ToString();

            var token = _jwtService.GenerateToken(user);

            return new AuthLoginResultDTO
            {
                Token = token,
                ExpiresIn = 3600,
                SessionId = sessionId,
                OrganizationId = user.OrganizationId,
                User = new AuthLoginUserDTO
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Role = user.Role
                }
            };
        }

        public async Task RequestPasswordResetAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("E-mail é obrigatório.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return; // não revela se o e-mail existe

            var token = Guid.NewGuid().ToString("N");
            _context.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = user.Id,
                Token = token,
                Expiration = DateTime.UtcNow.AddHours(1)
            });
            await _context.SaveChangesAsync();

            var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://wisemonitor.vercel.app";
            var resetLink = $"{frontendUrl}/reset-password?token={token}";

            try
            {
                var templatePath = Path.Combine(AppContext.BaseDirectory, "html", "PasswordReset.html");
                var htmlBody = await File.ReadAllTextAsync(templatePath);

                htmlBody = htmlBody.Replace("{{UserName}}", user.FirstName ?? "Usuário");
                htmlBody = htmlBody.Replace("{{ResetLink}}", resetLink);

                await _emailService.SendEmailAsync(user.Email!, "Redefinição de Senha - WiseMonitor", htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao enviar e-mail de redefinição de senha para {Email}", user.Email);
            }
        }

        public async Task ResetPasswordAsync(string token, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
                throw new ArgumentException("Token e nova senha são obrigatórios.");

            var resetToken = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == token && !t.Used);

            if (resetToken == null || resetToken.Expiration < DateTime.UtcNow)
                throw new InvalidOperationException("Token inválido ou expirado.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == resetToken.UserId)
                ?? throw new InvalidOperationException("Usuário não encontrado.");

            user.PasswordHash = HashPassword(newPassword);
            resetToken.Used = true;

            await _context.SaveChangesAsync();
        }

        public async Task LogoutAsync(string bearerToken)
        {
            var userId = _jwtService.GetUserIdFromToken(bearerToken);
            if (userId == null)
                throw new UnauthorizedAccessException("Token inválido ou expirado.");

            var user = await _context.Users.FindAsync(userId.Value)
                ?? throw new UnauthorizedAccessException("Usuário não encontrado.");

            if (user.OrganizationId.HasValue)
                await _liveSessionService.EndSessionForOrganizationAsync(user.OrganizationId.Value);
        }
    }
}
