using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.Services;
using WiseMonitor.Api.DTOs;

namespace WiseMonitor.Api.Handlers
{
    public class LiveMonitoringHandler
    {
        private readonly ILiveMonitoringService _liveService;
        private readonly ILiveSessionService _liveSessionService;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public LiveMonitoringHandler(
            ILiveSessionService liveSessionService,
            ILiveMonitoringService liveMonitoringService)
        {
            _liveSessionService = liveSessionService ?? throw new ArgumentNullException(nameof(liveSessionService));
            _liveService = liveMonitoringService ?? throw new ArgumentNullException(nameof(liveMonitoringService));
        }

        public async Task HandleWebSocketAsync(
            WebSocket webSocket,
            string deviceId,
            string token,
            Guid organizationId,
            CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"\n[WS] 🔵 NOVA CONEXÃO → Device={deviceId}, Org={organizationId}");

            var buffer = new byte[1024 * 64];

            // Sessão fixa para a organização
            var sessionId = await _liveSessionService.GetOrCreateSessionForOrganizationAsync(organizationId);

            Console.WriteLine($"[WS] Sessão carregada → {sessionId}");

            // Registra este admin na sessão
            _liveService.RegisterAdmin(organizationId.ToString(), sessionId, webSocket);

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(buffer, cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine($"[WS] 🔴 CLOSE solicitado → Device={deviceId}");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", cancellationToken);
                        break;
                    }

                    if (result.MessageType != WebSocketMessageType.Text)
                        continue;

                    var jsonMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    Console.WriteLine(
                        $"\n[WS] 📩 Mensagem recebida do Device={deviceId} | {result.Count} bytes\n" +
                        $"Preview: {jsonMessage[..Math.Min(200, jsonMessage.Length)]}...\n"
                    );

                    try
                    {
                        var message = JsonSerializer.Deserialize<MonitoringMessageDto>(jsonMessage, _jsonOptions);
                        if (message == null)
                        {
                            Console.WriteLine("[WS] ⚠ Mensagem JSON inválida (deserialização retornou null).");
                            continue;
                        }

                        message.DeviceId ??= deviceId;
                        message.Timestamp = message.Timestamp == default ? DateTime.UtcNow : message.Timestamp;

                        Console.WriteLine($"[WS] Tipo detectado → {message.Type}");

                        // Atualiza cache de dispositivos
                        _liveService.RegisterOrUpdateDevice(new LiveDeviceUpdateDTO
                        {
                            DeviceId = message.DeviceId,
                            Username = message.Username,
                            Department = message.Department,
                            Status = message.Status ?? "online",
                            Timestamp = message.Timestamp
                        });

                        // ===========================
                        // ENCAMINHAMENTO P/ ADMIN (SIGNALING)
                        // ===========================
                        if (message.Type is "offer" or "answer" or "candidate")
                        {
                            Console.WriteLine(
                                $"[Signaling] 🔁 Repassando {message.Type.ToUpper()} | Device={deviceId} → OrgSession={sessionId}"
                            );

                            // ✔ CORREÇÃO CRÍTICA
                            await _liveService.BroadcastFrameAsync(deviceId, message);
                        }
                        else
                        {
                            Console.WriteLine($"[WS] Frame ignorado (não é sinalização): {message.Type}");
                        }
                    }
                    catch (JsonException jex)
                    {
                        Console.WriteLine($"[WS] ❌ Erro ao deserializar JSON → {jex.Message}");
                    }
                }
            }
            catch (WebSocketException wsex)
            {
                Console.WriteLine($"[WS] ❌ WebSocket encerrado abruptamente (Device={deviceId}) → {wsex.Message}");
            }
            finally
            {
                Console.WriteLine($"[WS] 🔴 DESCONECTADO → Device={deviceId}");
                _liveService.UnregisterAdmin(organizationId.ToString(), sessionId);
            }
        }
    }
}
