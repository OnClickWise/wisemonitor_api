using System;

namespace WiseMonitor.Api.Models
{
    public class RefreshToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        // Nunca guarda o token em texto puro: só o hash SHA-256, igual a uma senha.
        // Um dump da tabela sozinho não deixa ninguém se passar pelo agent.
        public string TokenHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RevokedAt { get; set; }

        // Encadeia a rotação: cada refresh troca o token por um novo e revoga este.
        public string? ReplacedByTokenHash { get; set; }

        public bool IsActive => RevokedAt == null && ExpiresAt > DateTime.UtcNow;
    }
}
