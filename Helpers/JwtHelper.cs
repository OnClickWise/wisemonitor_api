using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WiseMonitor.Api.Config;

namespace WiseMonitor.Api.Helpers
{
    public class JwtHelper
    {
        private readonly JwtSettings _jwtSettings;
        private readonly byte[] _key;

        public JwtHelper(JwtSettings jwtSettings)
        {
            _jwtSettings = jwtSettings ?? throw new ArgumentNullException(nameof(jwtSettings));
            _key = Encoding.ASCII.GetBytes(_jwtSettings.SecretKey);
        }

        public bool ValidateToken(string token, out ClaimsPrincipal? claimsPrincipal)
        {
            claimsPrincipal = null;

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(_key),
                    ValidateIssuer = !string.IsNullOrWhiteSpace(_jwtSettings.Issuer),
                    ValidateAudience = !string.IsNullOrWhiteSpace(_jwtSettings.Audience),
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidAudience = _jwtSettings.Audience,
                    ClockSkew = TimeSpan.Zero
                };

                claimsPrincipal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
