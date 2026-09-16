using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;

namespace WiseMonitor.Api.Services
{
    public interface ILiveMonitoringService
    {
        // --- Atualização de devices ativos ---
        void UpdateDevice(string deviceId, string orgId, string username, string department,
                          string thumbnailUrl, string fullScreenUrl, string type = "screenshot", string payload = "");

        void RegisterOrUpdateDevice(LiveDeviceUpdateDTO dto);

        // --- Correção manual do usuário associado (sem esperar o próximo screenshot) ---
        void UpdateDeviceUser(string deviceId, string userId, string username);

        // --- Controle de Admins conectados (visualizadores) com sessionId ---
        void RegisterAdmin(string orgId, string sessionId, WebSocket adminSocket);
        void UnregisterAdmin(string orgId, string sessionId);

        // --- Controle de "quem está assistindo" cada device (bookkeeping usado pelo dashboard) ---
        void AddWatcher(string deviceId, string sessionId);
        void RemoveWatcher(string deviceId, string sessionId);
        void RemoveWatcherFromAllDevices(string sessionId);

        /// <summary>
        /// Disparado quando um device ganha o primeiro espectador ou perde o último —
        /// usado por LiveFrameRelay para avisar o agent (via /ws/agent-stream) quando
        /// vale a pena capturar/enviar frames, sem precisar de polling HTTP.
        /// </summary>
        event Action<string, bool>? WatchStateChanged;

        bool IsWatched(string deviceId);

        /// <summary>
        /// Sockets dos admins que estão assistindo este device agora — usado por
        /// LiveFrameRelay para repassar o frame só a quem está olhando.
        /// </summary>
        IReadOnlyList<WebSocket> GetWatcherSockets(string deviceId);

        // --- Leitura de estado/cache ---
        IReadOnlyList<MonitoringMessageDto> GetCachedMessages(string orgId);
        MonitoringMessageDto? GetLiveDevice(string deviceId);
        IReadOnlyCollection<MonitoringMessageDto> GetAllLiveDevices();

        // --- Presença (online/offline real) ---
        /// <summary>Desligamento anunciado pelo agent: reflete offline na hora.</summary>
        void MarkDeviceOffline(string deviceId);

        /// <summary>
        /// Reavalia todos os devices e avisa os admins de qualquer um que expirou
        /// (sem sinal recente). Chamado periodicamente por um sweeper — sem isso o
        /// painel só perceberia o offline no próximo evento de outro device.
        /// </summary>
        IReadOnlyCollection<string> BroadcastExpiredDevices();

        // --- Broadcast / Signaling ---
        Task BroadcastFrameAsync(string deviceId, MonitoringMessageDto frame);

        // --- Video segments: notifica dashboards que um novo segmento está disponível ---
        Task NotifyNewSegmentAsync(string deviceId, string orgId, Guid segmentId, DateTime startedAt, DateTime endedAt);
    }
}
