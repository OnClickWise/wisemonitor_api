using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Handlers
{
    public class LiveWebSocketHandler
    {
        private readonly LiveStreamHub _hub;

        public LiveWebSocketHandler(LiveStreamHub hub)
        {
            _hub = hub;
        }

        public async Task HandleAsync(HttpContext context, Guid organizationId, string sessionId)
        {
            var sessionKey = $"{organizationId}:{sessionId}";
            using var socket = await context.WebSockets.AcceptWebSocketAsync();

            var buffer = new byte[8192];
            LiveClient? client = null;
            Guid clientId = Guid.Empty;

            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var signal = JsonSerializer.Deserialize<WebRtcSignalDTO>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (signal == null || string.IsNullOrWhiteSpace(signal.Type))
                        continue;

                    var type = signal.Type.ToLowerInvariant();

                    // ============================
                    // HELLO
                    // ============================
                    if (type == "hello")
                    {
                        if (client != null)
                            _hub.RemoveClient(client.SessionKey, clientId);

                        var role = signal.Role?.ToLower() == "viewer"
                            ? LiveRole.Viewer
                            : LiveRole.Producer;

                        var userId = signal.DeviceId ?? Guid.NewGuid().ToString();

                        client = new LiveClient(
                            socket,
                            role,
                            organizationId,
                            sessionId,
                            userId
                        )
                        { HasSentHello = true };

                        clientId = _hub.AddClient(client);

                        Console.WriteLine($"[WS] HELLO | {role} | {client.SessionKey}");

                        // avisos de controle
                        var controlSignal = new WebRtcSignalDTO
                        {
                            Type = role == LiveRole.Viewer ? "viewer-join" : "start-producer",
                            SessionId = sessionId
                        };

                        await _hub.BroadcastControlAsync(
                            client.SessionKey,
                            controlSignal,
                            clientId
                        );

                        continue;
                    }

                    if (client == null || !client.HasSentHello)
                        continue;

                    // ============================
                    // PING
                    // ============================
                    if (type == "ping")
                    {
                        _hub.UpdatePing(client.SessionKey, clientId);
                        continue;
                    }

                    // ============================
                    // SIGNAL ROUTING
                    // ============================
                    signal.SessionId ??= sessionId;
                    signal.From = client.Role.ToString().ToLowerInvariant();

                    await _hub.BroadcastControlAsync(
                        client.SessionKey,
                        signal,
                        clientId
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WS ❌] {ex.Message}");
            }
            finally
            {
                if (client != null)
                {
                    _hub.RemoveClient(client.SessionKey, clientId);

                    var leftSignal = new WebRtcSignalDTO
                    {
                        Type = "viewer-left",
                        SessionId = sessionId
                    };

                    await _hub.BroadcastControlAsync(
                        client.SessionKey,
                        leftSignal,
                        clientId
                    );
                }

                if (socket.State == WebSocketState.Open)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Encerrado",
                        CancellationToken.None
                    );
                }
            }
        }
    }
}
