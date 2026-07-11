using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Services
{
    public interface IAuthService
    {
        Task<LoginResponseDTO> LoginAsync(LoginRequestDTO LoginDto);
        string HashPassword(string password);
        bool VerifyPassword(string password, string hashedPassword);

        /// <summary>Autentica por e-mail/senha (sem seleção prévia de organização) e emite o JWT real da aplicação.</summary>
        Task<AuthLoginResultDTO> LoginByEmailAsync(string email, string password);

        /// <summary>Gera um token de redefinição de senha e envia o e-mail correspondente, se o usuário existir.</summary>
        Task RequestPasswordResetAsync(string email);

        Task ResetPasswordAsync(string token, string newPassword);

        /// <summary>Encerra a sessão de monitoramento ao vivo da organização do usuário autenticado pelo Bearer token.</summary>
        Task LogoutAsync(string bearerToken);
    }
}
