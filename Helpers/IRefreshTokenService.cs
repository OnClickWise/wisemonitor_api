using System;
using System.Threading.Tasks;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Helpers
{
    public interface IRefreshTokenService
    {
        /// <summary>Gera e persiste um novo refresh token para o usuário; devolve o valor cru (só existe uma vez).</summary>
        Task<string> IssueAsync(Guid userId);

        /// <summary>
        /// Valida o token cru recebido do agent, revoga-o e emite um substituto (rotação).
        /// Devolve null se o token não existir, já tiver sido usado/revogado ou tiver expirado.
        /// </summary>
        Task<(User User, string NewRefreshToken)?> RedeemAsync(string rawToken);
    }
}
