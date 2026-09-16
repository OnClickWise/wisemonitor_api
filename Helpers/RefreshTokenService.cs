using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WiseMonitor.Api.Data;
using WiseMonitor.Api.Models;

namespace WiseMonitor.Api.Helpers
{
    /// <summary>
    /// Fecha a lacuna do JWT sem refresh: o access token dura pouco tempo, mas o
    /// agent desktop precisa continuar rodando (e monitorando) por dias sem que
    /// ninguém abra o app pra logar de novo. Este token é opaco (não é JWT), dura
    /// muito mais e só serve pra trocar por um access token novo em /auth/refresh.
    ///
    /// Rotação: cada troca revoga o token usado e emite um substituto. Um token
    /// revogado sendo reapresentado é sinal de token roubado/replay — hoje só loga,
    /// não derruba os demais tokens do usuário (não há essa granularidade ainda).
    /// </summary>
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly AppDbContext _context;
        private readonly int _expirationDays;

        public RefreshTokenService(AppDbContext context, IConfiguration config)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _expirationDays = int.TryParse(config["Jwt:RefreshTokenExpirationDays"], out var days) ? days : 3650;
        }

        public async Task<string> IssueAsync(Guid userId)
        {
            var raw = GenerateRawToken();

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                TokenHash = Hash(raw),
                ExpiresAt = DateTime.UtcNow.AddDays(_expirationDays)
            });

            await _context.SaveChangesAsync();
            return raw;
        }

        public async Task<(User User, string NewRefreshToken)?> RedeemAsync(string rawToken)
        {
            if (string.IsNullOrWhiteSpace(rawToken))
                return null;

            var hash = Hash(rawToken);

            var existing = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == hash);

            if (existing == null || !existing.IsActive)
                return null;

            var user = await _context.Users.FindAsync(existing.UserId);
            if (user == null || !user.IsActive)
                return null;

            var newRaw = GenerateRawToken();
            var newHash = Hash(newRaw);

            existing.RevokedAt = DateTime.UtcNow;
            existing.ReplacedByTokenHash = newHash;

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = newHash,
                ExpiresAt = DateTime.UtcNow.AddDays(_expirationDays)
            });

            await _context.SaveChangesAsync();

            return (user, newRaw);
        }

        private static string GenerateRawToken() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        private static string Hash(string raw) =>
            Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));
    }
}
