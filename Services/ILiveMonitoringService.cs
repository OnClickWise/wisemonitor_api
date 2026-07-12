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

        // --- Controle de Admins conectados (visualizadores) com sessionId ---
        void RegisterAdmin(string orgId, string sessionId, WebSocket adminSocket);
        void UnregisterAdmin(string orgId, string sessionId);

        // --- Controle de "quem está assistindo" cada device (bookkeeping usado pelo dashboard) ---
        void AddWatcher(string deviceId, string sessionId);
        void RemoveWatcher(string deviceId, string sessionId);
        void RemoveWatcherFromAllDevices(string sessionId);

        // --- Leitura de estado/cache ---
        IReadOnlyList<MonitoringMessageDto> GetCachedMessages(string orgId);
        MonitoringMessageDto? GetLiveDevice(string deviceId);
        IReadOnlyCollection<MonitoringMessageDto> GetAllLiveDevices();

        // --- Broadcast / Signaling ---
        Task BroadcastFrameAsync(string deviceId, MonitoringMessageDto frame);

        // --- Video segments: notifica dashboards que um novo segmento está disponível ---
        Task NotifyNewSegmentAsync(string deviceId, string orgId, Guid segmentId, DateTime startedAt, DateTime endedAt);
    }
}
