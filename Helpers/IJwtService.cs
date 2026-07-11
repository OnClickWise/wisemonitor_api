using System.Security.Claims;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Helpers
{
    public interface IJwtService
    {
        string GenerateToken(User user);
        string GenerateToken(User user, Guid orgId);
        ClaimsPrincipal? ValidateToken(string token);
        
        // 🔹 Novo método: extrair ID do usuário de um token
        Guid? GetUserIdFromToken(string token);
    }
}
