using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WiseMonitor.Api.DTOs;
using WiseMonitor.Api.Services;
using System.Linq;

namespace WiseMonitor.Api.Middlewares
{
    public class DeviceWebSocketMiddleware
    {
        private readonly RequestDelegate _next;

        public DeviceWebSocketMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest
                || !context.Request.Path.StartsWithSegments("/ws/live"))
            {
                await _next(context);
                return;
            }

            if (context.User?.Claims != null)
            {
                Console.WriteLine("--- 🔍 Claims recebidos no WebSocket ---");
                foreach (var claim in context.User.Claims)
                {
                    Console.WriteLine($"Key: '{claim.Type}' | Value: '{claim.Value}'");
                }
                Console.WriteLine("----------------------------------------");
            }

            // 🔐 TENTATIVA 1: Busca flexível (organizationId, OrganizationId ou orgId)
            var orgClaim = context.User?.Claims
                .FirstOrDefault(c => c.Type.Equals("organizationId", StringComparison.OrdinalIgnoreCase) 
                                  || c.Type.Equals("OrganizationId", StringComparison.OrdinalIgnoreCase)
                                  || c.Type.Equals("orgId", StringComparison.OrdinalIgnoreCase))?.Value; // <--- ADICIONADO AQUI

            // 🔐 TENTATIVA 2: Se falhar, busca pelo schema padrão do .NET
            if (string.IsNullOrEmpty(orgClaim))
            {
                 // Tenta schema de nome (comum em Identity)
                 orgClaim = context.User?.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/organizationId")?.Value
                         ?? context.User?.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/organizationId")?.Value;
            }

            // Validação final
            if (string.IsNullOrEmpty(orgClaim) || !Guid.TryParse(orgClaim, out Guid organizationId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("organizationId não encontrado ou inválido no token.");
                Console.WriteLine($"[WS ❌] Falha ao ler OrganizationId. Valor lido: '{orgClaim ?? "NULO"}'");
                return;
            }

            var hub = context.RequestServices.GetRequiredService<LiveStreamHub>();
            var liveSessionService = context.RequestServices.GetRequiredService<ILiveSessionService>();

            var sessionId = await liveSessionService.GetOrCreateSessionForOrganizationAsync(organizationId);

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var buffer = new byte[8192];
            var ct = CancellationToken.None;

            LiveClient? client = null;
            Guid clientId = Guid.Empty;

            Console.WriteLine($"[WS] ✅ Conexão aceita | Org={organizationId} | Sessão={sessionId}");

            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(buffer, ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (string.IsNullOrWhiteSpace(message))
                        continue;

                    var signal = JsonSerializer.Deserialize<WebRtcSignalDTO>(message,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (signal == null) continue;

                    var type = signal.Type?.ToLowerInvariant();

                    // ============================
                    // HELLO → REGISTRO
                    // ============================
                    if (type == "hello")
                    {
                        if (client != null)
                            hub.RemoveClient(client.SessionKey, clientId);

                        var role = signal.Role?.ToLower() == "viewer"
                            ? LiveRole.Viewer
                            : LiveRole.Producer;

                        var userId = signal.DeviceId
                            ?? context.Connection.RemoteIpAddress?.ToString()
                            ?? Guid.NewGuid().ToString();

                        client = new LiveClient(
                            socket,
                            role,
                            organizationId,
                            sessionId,
                            userId
                        )
                        { HasSentHello = true };

                        clientId = hub.AddClient(client);

                        Console.WriteLine($"[WS] 📡 HELLO | Org={organizationId} | Sess={sessionId} | Role={role}");
                        continue;
                    }

                    if (client == null || !client.HasSentHello)
                        continue;

                    // ============================
                    // PING
                    // ============================
                    if (type == "ping")
                    {
                        hub.UpdatePing(client.SessionKey, clientId);
                        continue;
                    }

                    // ============================
                    // WEBRTC SIGNALING
                    // ============================
                    signal.From = client.Role.ToString().ToLower();
                    signal.SessionId ??= client.SessionId;

                    await hub.BroadcastControlAsync(client.SessionKey, signal, clientId, ct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WS ❌] Erro | Org={organizationId} | {ex.Message}");
            }
            finally
            {
                if (client != null)
                    hub.RemoveClient(client.SessionKey, clientId);

                if (socket.State == WebSocketState.Open)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Encerrado", ct);

                Console.WriteLine($"[WS 🔴] Conexão encerrada | Org={organizationId}");
            }
        }
    }
}
