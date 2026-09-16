using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WiseMonitor.Api.DTOs;

namespace WiseMonitor.Api.Services
{
    public class LiveMonitoringService : ILiveMonitoringService
    {
        private readonly ILogger<LiveMonitoringService> _logger;

        public LiveMonitoringService(ILogger<LiveMonitoringService> logger)
        {
            _logger = logger;
        }

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

        /// <summary>
        /// Disparado quando um device ganha o primeiro espectador ou perde o último.
        /// É o que permite ao agent (via /ws/agent-stream, LiveFrameRelay) saber quando
        /// vale a pena capturar/enviar frames, sem precisar de polling HTTP.
        /// </summary>
        public event Action<string, bool>? WatchStateChanged;

        /// <summary>
        /// Janela sem nenhum sinal do device até ele ser considerado offline. Precisa
        /// ser folgada o bastante para absorver um heartbeat perdido, mas curta o
        /// bastante para o painel refletir um desligamento em pouco tempo.
        /// </summary>
        public static readonly TimeSpan PresenceTtl = TimeSpan.FromSeconds(90);

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
            _logger.LogDebug(
                "[DeviceUpdate] Org={OrgId}, Device={DeviceId}, User={Username}, Type={Type}, Thumb={ThumbLength} chars",
                orgId, deviceId, username ?? "Unknown", type, thumbnailUrl?.Length ?? 0);

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

        public void RegisterOrUpdateDevice(LiveDeviceUpdateDTO dto)
        {
            var orgId = dto.OrgId ?? "default";

            _logger.LogDebug(
                "[DeviceRegister] Org={OrgId}, Device={DeviceId}, Status={Status}, ThumbSize={ThumbSize} FullSize={FullSize}",
                orgId, dto.DeviceId, dto.Status ?? "online", dto.ThumbnailUrl?.Length ?? 0, dto.FullScreenUrl?.Length ?? 0);

            // Nem toda chamada resolve o UserId (ex.: heartbeat de um device que
            // ainda não teve nenhum screenshot) — preserva o que já estava em
            // cache em vez de apagar a identidade conhecida com um valor vazio.
            _liveDevices.TryGetValue(dto.DeviceId, out var existing);
            var userId = !string.IsNullOrWhiteSpace(dto.UserId) ? dto.UserId : existing?.UserId ?? "";

            var message = new MonitoringMessageDto
            {
                OrgId = orgId,
                DeviceId = dto.DeviceId,
                UserId = userId,
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
            _logger.LogInformation(
                "[AdminConnected] Org={OrgId}, Session={SessionId}, SocketState={SocketState}",
                orgId, sessionId, adminSocket.State);

            var orgAdmins = _adminSockets.GetOrAdd(orgId, _ => new ConcurrentDictionary<string, WebSocket>());
            orgAdmins[sessionId] = adminSocket;

            _logger.LogInformation("[AdminCount] Org={OrgId} agora tem {Count} admins conectados.", orgId, orgAdmins.Count);
        }

        public void UnregisterAdmin(string orgId, string sessionId)
        {
            _logger.LogInformation("[AdminDisconnected] Org={OrgId}, Session={SessionId}", orgId, sessionId);

            if (_adminSockets.TryGetValue(orgId, out var orgAdmins))
            {
                orgAdmins.TryRemove(sessionId, out _);
                _logger.LogInformation("[AdminCount] Org={OrgId} agora tem {Count} admins conectados.", orgId, orgAdmins.Count);
            }
        }

        // ================================
        // WATCHERS (quem está assistindo cada device agora)
        // ================================
        public void AddWatcher(string deviceId, string sessionId)
        {
            var set = _watchers.GetOrAdd(deviceId, _ => new ConcurrentDictionary<string, byte>());
            var wasEmpty = set.IsEmpty;
            set[sessionId] = 0;
            _logger.LogInformation("[Watcher+] Device={DeviceId}, Session={SessionId}, Total={Total}", deviceId, sessionId, set.Count);

            if (wasEmpty)
                WatchStateChanged?.Invoke(deviceId, true);
        }

        public void RemoveWatcher(string deviceId, string sessionId)
        {
            if (!_watchers.TryGetValue(deviceId, out var set))
                return;

            set.TryRemove(sessionId, out _);
            _logger.LogInformation("[Watcher-] Device={DeviceId}, Session={SessionId}, Total={Total}", deviceId, sessionId, set.Count);

            if (!set.IsEmpty)
                return;

            _watchers.TryRemove(deviceId, out _);
            WatchStateChanged?.Invoke(deviceId, false);
        }

        public void RemoveWatcherFromAllDevices(string sessionId)
        {
            foreach (var (deviceId, set) in _watchers)
            {
                set.TryRemove(sessionId, out _);

                if (!set.IsEmpty) continue;

                _watchers.TryRemove(deviceId, out _);
                WatchStateChanged?.Invoke(deviceId, false);
            }
        }

        public bool IsWatched(string deviceId) =>
            _watchers.TryGetValue(deviceId, out var set) && !set.IsEmpty;

        /// <summary>
        /// Sockets dos admins que estão com a tela deste device aberta agora. O relay
        /// usa isso para mandar o frame só a quem está olhando, em vez de transmitir
        /// para toda a organização.
        /// </summary>
        public IReadOnlyList<WebSocket> GetWatcherSockets(string deviceId)
        {
            if (!_watchers.TryGetValue(deviceId, out var deviceWatchers) || deviceWatchers.IsEmpty)
                return Array.Empty<WebSocket>();

            var sockets = new List<WebSocket>();

            // O sessionId do watcher é a chave do socket dentro da org; como o watcher
            // não guarda a org, procura em todas (são poucas conexões de admin).
            foreach (var orgAdmins in _adminSockets.Values)
            {
                foreach (var sessionId in deviceWatchers.Keys)
                {
                    if (orgAdmins.TryGetValue(sessionId, out var socket))
                        sockets.Add(socket);
                }
            }

            return sockets;
        }

        // ================================
        // READS
        // ================================
        public IReadOnlyList<MonitoringMessageDto> GetCachedMessages(string orgId)
        {
            _logger.LogDebug("[GetCache] Org={OrgId}", orgId);

            if (_messageCache.TryGetValue(orgId, out var messages))
            {
                lock (messages)
                    return messages.ToList();
            }

            return Array.Empty<MonitoringMessageDto>();
        }

        public MonitoringMessageDto? GetLiveDevice(string deviceId)
        {
            _logger.LogDebug("[GetLiveDevice] Device={DeviceId}", deviceId);
            _liveDevices.TryGetValue(deviceId, out var device);
            return device == null ? null : WithFreshnessApplied(device);
        }

        public IReadOnlyCollection<MonitoringMessageDto> GetAllLiveDevices()
        {
            _logger.LogDebug("[GetAllDevices]");
            return _liveDevices.Values.Select(WithFreshnessApplied).ToList();
        }

        /// <summary>
        /// O dicionário só cresce: um device que parou de enviar continuaria com
        /// Status="online" indefinidamente, e o painel confia nesse campo. Em vez de
        /// apagar a entrada (o que faria o card sumir da tela), o status é derivado
        /// da idade do último sinal — o device continua listado, porém offline. Nunca
        /// muta o cache: mutar a instância impediria o device de voltar a "online"
        /// quando um sinal novo chegasse reaproveitando o mesmo objeto.
        /// </summary>
        private static MonitoringMessageDto WithFreshnessApplied(MonitoringMessageDto device)
        {
            var isFresh = DateTime.UtcNow - device.Timestamp <= PresenceTtl;
            var status = isFresh ? device.Status : "offline";

            if (status == device.Status)
                return device;

            return new MonitoringMessageDto
            {
                OrgId = device.OrgId,
                UserId = device.UserId,
                DeviceId = device.DeviceId,
                Hostname = device.Hostname,
                Ip = device.Ip,
                Type = device.Type,
                Payload = device.Payload,
                Username = device.Username,
                Department = device.Department,
                ThumbnailUrl = device.ThumbnailUrl,
                FullScreenUrl = device.FullScreenUrl,
                Status = status,
                Timestamp = device.Timestamp
            };
        }

        /// <summary>
        /// Corrige o nome do usuário exibido pro device já em cache, sem esperar o
        /// próximo screenshot — usado quando um admin corrige manualmente qual
        /// usuário está sendo monitorado numa máquina (ex.: agent configurado com
        /// a pessoa errada). Se o device ainda não apareceu no cache (nunca
        /// mandou nada), não há o que corrigir aqui — o valor certo já vem do
        /// banco na próxima vez que algo chegar.
        /// </summary>
        public void UpdateDeviceUser(string deviceId, string userId, string username)
        {
            if (!_liveDevices.TryGetValue(deviceId, out var device))
                return;

            device.UserId = userId;
            device.Username = username;
            _liveDevices[deviceId] = device;

            _ = BroadcastFrameAsync(deviceId, device);
        }

        /// <summary>Desligamento anunciado pelo agent: reflete offline na hora.</summary>
        public void MarkDeviceOffline(string deviceId)
        {
            if (!_liveDevices.TryGetValue(deviceId, out var device))
                return;

            device.Status = "offline";
            _liveDevices[deviceId] = device;

            _logger.LogInformation("[DeviceOffline] Device={DeviceId}", deviceId);
            _ = BroadcastFrameAsync(deviceId, device);
        }

        /// <summary>
        /// Reavalia todo mundo e avisa os admins se alguém expirou. Chamado pelo
        /// varredor (DevicePresenceSweeper) — sem isso o painel só perceberia o
        /// offline no próximo evento de outro device, que pode nunca vir se a org
        /// toda desligou.
        /// </summary>
        public IReadOnlyCollection<string> BroadcastExpiredDevices()
        {
            var expired = new List<string>();
            var orgsToNotify = new HashSet<string>();

            foreach (var (deviceId, device) in _liveDevices)
            {
                if (device.Status == "offline") continue;
                if (DateTime.UtcNow - device.Timestamp <= PresenceTtl) continue;

                device.Status = "offline";
                _liveDevices[deviceId] = device;
                expired.Add(deviceId);
                orgsToNotify.Add(device.OrgId ?? "default");
            }

            foreach (var orgId in orgsToNotify)
                _ = BroadcastToOrgAsync(orgId);

            return expired;
        }

        private async Task BroadcastToOrgAsync(string orgId)
        {
            if (!_adminSockets.TryGetValue(orgId, out var admins) || !admins.Any())
                return;

            var allDevices = _liveDevices.Values
                .Where(d => (d.OrgId ?? "default") == orgId)
                .Select(WithFreshnessApplied)
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

        // ================================
        // VIDEO SEGMENTS — notifica dashboards conectados que um novo segmento
        // ficou disponível para o device (reaproveita o mesmo canal /ws/monitor).
        // ================================
        public async Task NotifyNewSegmentAsync(string deviceId, string orgId, Guid segmentId, DateTime startedAt, DateTime endedAt)
        {
            if (!_adminSockets.TryGetValue(orgId, out var admins) || !admins.Any())
                return;

            var wrapper = new
            {
                eventType = "segment",
                deviceId,
                segmentId,
                startedAt,
                endedAt
            };
            var json = JsonSerializer.Serialize(wrapper, _jsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);

            foreach (var (sessionId, ws) in admins.ToList())
            {
                if (ws.State != WebSocketState.Open)
                    continue;

                try
                {
                    await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[NotifyNewSegment] Falha ao notificar sessão {SessionId}", sessionId);
                }
            }
        }

        // ================================
        // BROADCAST
        // ================================
        public async Task BroadcastFrameAsync(string deviceId, MonitoringMessageDto frame)
        {
            var orgId = frame.OrgId ?? "default";

            _logger.LogDebug("[Broadcast] Org={OrgId}, Device={DeviceId}", orgId, deviceId);

            if (!_adminSockets.TryGetValue(orgId, out var admins) || !admins.Any())
            {
                _logger.LogDebug("[Broadcast] Nenhum admin conectado na org {OrgId}", orgId);
                return;
            }

            // Send ALL devices for this org so the frontend always has a full consistent state
            var allDevices = _liveDevices.Values
                .Where(d => d.OrgId == orgId)
                .Select(WithFreshnessApplied)
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
