using System;

namespace WiseMonitor.Api.DTOs
{
    /// <summary>
    /// Device com o nome/e-mail do usuário já resolvido (atribuição manual tem
    /// prioridade sobre o usuário detectado pela última screenshot), usado pela
    /// tela "Dispositivos > Ao Vivo" para não precisar cruzar dados no frontend.
    /// </summary>
    public class DeviceWithUserDto
    {
        public Guid Id { get; set; }
        public string Hostname { get; set; } = string.Empty;
        public string? IpAddress { get; set; }
        public bool IsOnline { get; set; }
        public DateTime LastSeen { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public Guid? UserId { get; set; }
    }
}
