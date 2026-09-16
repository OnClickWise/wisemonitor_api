using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WiseMonitor.Api.Helpers;
using WiseMonitor.Api.Services;

namespace WiseMonitor.Api.Middlewares
{
    /// <summary>
    /// WebSocket /ws/agent-stream — canal de tela ao vivo do agent.
    ///
    /// O agent mantém esta conexão aberta e empurra frames JPEG binários; o backend
    /// repassa direto a quem está assistindo (LiveFrameRelay). No sentido inverso
    /// trafegam apenas comandos curtos ("watch-start"/"watch-stop"), que dizem ao agent
    /// quando vale a pena capturar.
    /// </summary>
    public class AgentStreamWebSocketMiddleware
    {
        /// <summary>
        /// Frame de 1280px em JPEG fica bem abaixo disso; o teto existe só para não
        /// aceitar uma mensagem absurda de um cliente que se comporte mal.
        /// </summary>
        private const int MaxFrameBytes = 4 * 1024 * 1024;

        private readonly RequestDelegate _next;
        private readonly ILogger<AgentStreamWebSocketMiddleware> _logger;

        public AgentStreamWebSocketMiddleware(RequestDelegate next, ILogger<AgentStreamWebSocketMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest
                || !context.Request.Path.StartsWithSegments("/ws/agent-stream"))
            {
                await _next(context);
                return;
            }

            var token = context.Request.Query["token"].FirstOrDefault();
            if (string.IsNullOrEmpty(token))
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) &&
                    authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    token = authHeader["Bearer ".Length..];
            }

            if (string.IsNullOrEmpty(token))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Token ausente.");
                return;
            }

            var jwtHelper = context.RequestServices.GetRequiredService<JwtHelper>();
            if (!jwtHelper.ValidateToken(token, out var principal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Token inválido.");
                return;
            }

            var orgClaim = principal?.Claims.FirstOrDefault(c =>
                c.Type.Equals("organizationId", StringComparison.OrdinalIgnoreCase) ||
                c.Type.Equals("orgId", StringComparison.OrdinalIgnoreCase))?.Value;

            if (!Guid.TryParse(orgClaim, out var orgGuid))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("organizationId inválido no token.");
                return;
            }

            var deviceId = context.Request.Query["deviceId"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("deviceId ausente.");
                return;
            }

            var orgId = orgGuid.ToString();
            var relay = context.RequestServices.GetRequiredService<LiveFrameRelay>();

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            relay.RegisterAgent(deviceId, orgId, socket);

            // Buffer reaproveitado entre frames: alocar ~200KB por quadro a 15fps
            // colocaria pressão desnecessária no GC do backend.
            var buffer = new byte[64 * 1024];
            var frame = new System.IO.MemoryStream(256 * 1024);

            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    frame.SetLength(0);

                    WebSocketReceiveResult result;
                    var oversized = false;

                    do
                    {
                        result = await socket.ReceiveAsync(buffer, CancellationToken.None);

                        if (result.MessageType == WebSocketMessageType.Close)
                            return;

                        if (result.MessageType != WebSocketMessageType.Binary)
                            break;

                        if (frame.Length + result.Count > MaxFrameBytes)
                        {
                            oversized = true;
                            continue; // segue drenando até o fim da mensagem
                        }

                        frame.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType != WebSocketMessageType.Binary)
                        continue; // ping/controle vindo do agent — ignora

                    if (oversized)
                    {
                        _logger.LogWarning("[AgentStream] Frame acima do limite descartado | Device={DeviceId}", deviceId);
                        continue;
                    }

                    if (frame.Length == 0) continue;

                    await relay.RelayFrameAsync(
                        deviceId, orgId,
                        new ArraySegment<byte>(frame.GetBuffer(), 0, (int)frame.Length));
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogInformation("[AgentStream] Conexão encerrada abruptamente | Device={DeviceId} | {Message}", deviceId, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AgentStream] Erro | Device={DeviceId}", deviceId);
            }
            finally
            {
                relay.UnregisterAgent(deviceId, socket);

                if (socket.State == WebSocketState.Open)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Encerrado", CancellationToken.None);
            }
        }
    }
}
