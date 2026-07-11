using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WiseMonitor.Api.Models;
using WiseMonitor.Api.Models.Enums;
using System.Linq;

namespace WiseMonitor.Api.Helpers
{
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _config;

        public JwtService(IConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public string GenerateToken(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            var secretKey = _config["Jwt:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
                throw new InvalidOperationException("JWT SecretKey não configurada.");

            var issuer  = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];
            var expiresInMinutes = int.TryParse(_config["Jwt:ExpirationMinutes"], out var minutes) ? minutes : 60;

            // Normaliza o papel para o valor canônico
            var role = user.IsSuperAdmin
                ? UserRoles.SuperAdmin
                : UserRoles.Normalize(user.Role);

            var claimList = new System.Collections.Generic.List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                // "role" (short key) para que o frontend leia payload.role diretamente
                new Claim("role",         role),
                // ClaimTypes.Role para que UseAuthorization / [Authorize(Roles="...")] funcione
                new Claim(ClaimTypes.Role, role),
                new Claim("isSuperAdmin", user.IsSuperAdmin.ToString().ToLower()),
            };

            // orgId só para usuários com organização; SuperAdmin não tem org
            if (user.OrganizationId.HasValue && user.OrganizationId != Guid.Empty)
                claimList.Add(new Claim("orgId", user.OrganizationId.Value.ToString()));

            var claims = claimList.ToArray();

            var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer:            issuer,
                audience:          audience,
                claims:            claims,
                expires:           DateTime.UtcNow.AddMinutes(expiresInMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateToken(User user, Guid orgId)
        {
            user.OrganizationId = orgId;
            return GenerateToken(user);
        }

        public Guid? GetUserIdFromToken(string authorizationHeader)
        {
            if (string.IsNullOrWhiteSpace(authorizationHeader) ||
                !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return null;

            var tokenString = authorizationHeader[7..];

            try
            {
                var handler   = new JwtSecurityTokenHandler();
                var jwtToken  = handler.ReadJwtToken(tokenString);
                var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub);

                return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId)
                    ? userId
                    : null;
            }
            catch
            {
                return null;
            }
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            var secretKey = _config["Jwt:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
                throw new InvalidOperationException("JWT SecretKey não configurada.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var tokenHandler = new JwtSecurityTokenHandler();

            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer           = false,
                    ValidateAudience         = false,
                    ValidateLifetime         = true,
                    IssuerSigningKey         = key,
                    ValidateIssuerSigningKey = true,
                    ClockSkew                = TimeSpan.Zero
                }, out _);

                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}
