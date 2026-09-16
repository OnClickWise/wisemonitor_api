using System;

namespace WiseMonitor.Api.DTOs
{
    /// <summary>
    /// Batida de presença enviada pelo agent a cada poucos segundos enquanto a
    /// máquina está ligada. É o que sustenta o status online — o resto (screenshot,
    /// app-focus) depende do monitoramento estar ativo e não serve como sinal de
    /// "a máquina está ligada".
    /// </summary>
    public class DeviceHeartbeatDTO
    {
        /// <summary>Id persistido pelo agent. Vazio na primeira vez: o backend resolve pelo hostname.</summary>
        public Guid DeviceId { get; set; }

        public string? Hostname { get; set; }

        public string? IpAddress { get; set; }

        public string? AgentVersion { get; set; }
    }

    /// <summary>
    /// Aviso explícito de desligamento/logoff. Torna o offline imediato em vez de
    /// esperar o TTL do heartbeat expirar.
    /// </summary>
    public class DeviceOfflineDTO
    {
        public Guid DeviceId { get; set; }

        public string? Hostname { get; set; }
    }

    public class DevicePresenceResultDTO
    {
        public Guid DeviceId { get; set; }

        public bool IsOnline { get; set; }

        public DateTime LastSeen { get; set; }
    }
}
