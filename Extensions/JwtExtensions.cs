using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;


namespace WiseMonitor.Api.Extensions;


public static class JwtExtensions
{
public static IServiceCollection AddJwtAuth(this IServiceCollection services, IConfiguration config)
{
var key = config["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey not set");
var issuer = config["Jwt:Issuer"];
var audience = config["Jwt:Audience"];


services
.AddAuthentication(options =>
{
options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
options.RequireHttpsMetadata = false;
options.SaveToken = true;
options.TokenValidationParameters = new TokenValidationParameters
{
ValidateIssuerSigningKey = true,
IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
ValidIssuer = issuer,
ValidateAudience = !string.IsNullOrWhiteSpace(audience),
ValidAudience = audience,
ValidateLifetime = true,
ClockSkew = TimeSpan.FromSeconds(int.TryParse(config["Jwt:ClockSkewSeconds"], out var s) ? s : 60)
};
});


return services;
}
}