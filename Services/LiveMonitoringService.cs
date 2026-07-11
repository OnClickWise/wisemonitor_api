using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;

namespace WiseMonitor.Api.Services
{
    public class LiveMonitoringService : ILiveMonitoringService
    {
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly ConcurrentDictionary<string, List<MonitoringMessageDto>> _messageCache = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WebSocket>> _adminSockets = new();
        private readonly ConcurrentDictionary<string, MonitoringMessageDto> _liveDevices = new();

        // deviceId -> conjunto de sessionIds de viewers assistindo esse device agora
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _watchers = new();

        // ================================
        // DEVICE UPDATE
        // ================================
        public void UpdateDevice(
            string deviceId,
            string orgId,
            string? username,
            string? department,
            string? thumbnailUrl,
            string? fullScreenUrl,
            string type = "screenshot",
            string payload = "")
        {
            Console.WriteLine(
                $"[DeviceUpdate] Org={orgId}, Device={deviceId}, User={username ?? "Unknown"}, Type={type}, Thumb={(thumbnailUrl?.Length ?? 0)} chars"
            );

            var message = new MonitoringMessageDto
            {
                OrgId = orgId,
                DeviceId = deviceId,
                Type = type,
                Payload = payload,
                Username = username ?? "Unknown",
                Department = department ?? "Default",
                ThumbnailUrl = thumbnailUrl,
                FullScreenUrl = fullScreenUrl,
                Status = "online",
                Timestamp = DateTime.UtcNow
            };

            _liveDevices[deviceId] = message;

            var orgMessages = _messageCache.GetOrAdd(orgId, _ => new List<MonitoringMessageDto>());
            lock (orgMessages)
            {
                orgMessages.Add(message);
                if (orgMessages.Count > 50)
                    orgMessages.RemoveAt(0);
            }

            _ = BroadcastFrameAsync(deviceId, message);
        }

        public void UpdateDeviceScreen(string deviceId, string orgId, byte[] screenshotBytes, string contentType = "image/png")
        {
            Console.WriteLine(
                $"[ScreenUpdate] Org={orgId}, Device={deviceId}, Bytes={screenshotBytes.Length}"
            );

            var base64 = Convert.ToBase64String(screenshotBytes);

            // Preserva identidade já conhecida do device (vinda de um screenshot/update anterior)
            // em vez de sobrescrever com placeholders — frames ao vivo chegam a cada ~1s e não
            // devem "apagar" username/department já resolvidos.
            _liveDevices.TryGetValue(deviceId, out var existing);

            UpdateDevice(
                deviceId,
                orgId,
                username: existing?.Username ?? $"User-{deviceId}",
                department: existing?.Department ?? "Default",
                thumbnailUrl: $"data:{contentType};base64,{base64}",
                fullScreenUrl: $"data:{contentType};base64,{base64}",
                type: "screenshot",
                payload: base64
            );
        }

        public void RegisterOrUpdateDevice(LiveDeviceUpdateDTO dto)
        {
            var orgId = dto.OrgId ?? "default";

            Console.WriteLine(
                $"[DeviceRegister] Org={orgId}, Device={dto.DeviceId}, Status={dto.Status ?? "online"}, ThumbSize={(dto.ThumbnailUrl?.Length ?? 0)} FullSize={(dto.FullScreenUrl?.Length ?? 0)}"
            );

            var message = new MonitoringMessageDto
            {
                OrgId = orgId,
                DeviceId = dto.DeviceId,
                Username = dto.Username ?? "Unknown",
                Department = dto.Department ?? "Default",
                Status = dto.Status ?? "online",
                ThumbnailUrl = dto.ThumbnailUrl,
                FullScreenUrl = dto.FullScreenUrl,
                Timestamp = dto.Timestamp == default ? DateTime.UtcNow : dto.Timestamp
            };

            _liveDevices[dto.DeviceId] = message;

            _ = BroadcastFrameAsync(dto.DeviceId, message);
        }

        // ================================
        // ADMINS
        // ================================
        public void RegisterAdmin(string orgId, string sessionId, WebSocket adminSocket)
        {
            Console.WriteLine(
                $"[AdminConnected] Org={orgId}, Session={sessionId}, SocketState={adminSocket.State}"
            );

            var orgAdmins = _adminSockets.GetOrAdd(orgId, _ => new ConcurrentDictionary<string, WebSocket>());
            orgAdmins[sessionId] = adminSocket;

            Console.WriteLine($"[AdminCount] Org={orgId} agora tem {orgAdmins.Count} admins conectados.");
        }

        public void UnregisterAdmin(string orgId, string sessionId)
        {
            Console.WriteLine($"[AdminDisconnected] Org={orgId}, Session={sessionId}");

            if (_adminSockets.TryGetValue(orgId, out var orgAdmins))
            {
                orgAdmins.TryRemove(sessionId, out _);
                Console.WriteLine($"[AdminCount] Org={orgId} agora tem {orgAdmins.Count} admins conectados.");
            }
        }

        // ================================
        // WATCHERS (quem está assistindo cada device agora)
        // ================================
        public void AddWatcher(string deviceId, string sessionId)
        {
            var set = _watchers.GetOrAdd(deviceId, _ => new ConcurrentDictionary<string, byte>());
            set[sessionId] = 0;
            Console.WriteLine($"[Watcher+] Device={deviceId}, Session={sessionId}, Total={set.Count}");
        }

        public void RemoveWatcher(string deviceId, string sessionId)
        {
            if (_watchers.TryGetValue(deviceId, out var set))
            {
                set.TryRemove(sessionId, out _);
                Console.WriteLine($"[Watcher-] Device={deviceId}, Session={sessionId}, Total={set.Count}");
            }
        }

        public void RemoveWatcherFromAllDevices(string sessionId)
        {
            foreach (var (deviceId, set) in _watchers)
                set.TryRemove(sessionId, out _);
        }

        public bool IsWatched(string deviceId) =>
            _watchers.TryGetValue(deviceId, out var set) && !set.IsEmpty;

        // ================================
        // READS
        // ================================
        public IReadOnlyList<MonitoringMessageDto> GetCachedMessages(string orgId)
        {
            Console.WriteLine($"[GetCache] Org={orgId}");

            if (_messageCache.TryGetValue(orgId, out var messages))
            {
                lock (messages)
                    return messages.ToList();
            }

            return Array.Empty<MonitoringMessageDto>();
        }

        public MonitoringMessageDto? GetLiveDevice(string deviceId)
        {
            Console.WriteLine($"[GetLiveDevice] Device={deviceId}");
            _liveDevices.TryGetValue(deviceId, out var device);
            return device;
        }

        public IReadOnlyCollection<MonitoringMessageDto> GetAllLiveDevices()
        {
            Console.WriteLine("[GetAllDevices]");
            return _liveDevices.Values.ToList();
        }

        // ================================
        // BROADCAST
        // ================================
        public async Task BroadcastFrameAsync(string deviceId, MonitoringMessageDto frame)
        {
            var orgId = frame.OrgId ?? "default";

            Console.WriteLine($"[Broadcast] Org={orgId}, Device={deviceId}");

            if (!_adminSockets.TryGetValue(orgId, out var admins) || !admins.Any())
            {
                Console.WriteLine($"[Broadcast] Nenhum admin conectado na org {orgId}");
                return;
            }

            // Send ALL devices for this org so the frontend always has a full consistent state
            var allDevices = _liveDevices.Values
                .Where(d => d.OrgId == orgId)
                .ToList();

            var wrapper = new { eventType = "update", payload = allDevices };
            var json = JsonSerializer.Serialize(wrapper, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);

            var disconnected = new List<string>();

            foreach (var (sessionId, ws) in admins.ToList())
            {
                if (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch
                    {
                        disconnected.Add(sessionId);
                    }
                }
                else
                {
                    disconnected.Add(sessionId);
                }
            }

            foreach (var sessionId in disconnected)
                admins.TryRemove(sessionId, out _);
        }
    }
}
