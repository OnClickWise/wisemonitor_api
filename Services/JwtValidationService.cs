using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;


namespace WiseMonitor.Api.Services;


public class JwtValidationService
{
    private readonly IConfiguration _config;


    public JwtValidationService(IConfiguration config)
    {
        _config = config;
    }


    public (bool ok, ClaimsPrincipal? principal, string? error) ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, null, "Token vazio");


        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:SecretKey"]!));
            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = !string.IsNullOrWhiteSpace(_config["Jwt:Issuer"]),
                ValidIssuer = _config["Jwt:Issuer"],
                ValidateAudience = !string.IsNullOrWhiteSpace(_config["Jwt:Audience"]),
                ValidAudience = _config["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(int.TryParse(_config["Jwt:ClockSkewSeconds"], out var s) ? s : 60)
            };


            var principal = tokenHandler.ValidateToken(token, parameters, out _);
            return (true, principal, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }
}