using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
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
    /// WebSocket endpoint /ws/monitor — admin dashboard viewers.
    /// Frontend connects here to receive live device updates pushed by ScreenshotService.
    /// Message format sent to client: { eventType: "update", payload: [MonitoringMessageDto] }
    /// </summary>
    public class MonitorWebSocketMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<MonitorWebSocketMiddleware> _logger;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public MonitorWebSocketMiddleware(RequestDelegate next, ILogger<MonitorWebSocketMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest
                || !context.Request.Path.StartsWithSegments("/ws/monitor"))
            {
                await _next(context);
                return;
            }

            // --- JWT from query param or Authorization header ---
            var token = context.Request.Query["token"].FirstOrDefault();
            if (string.IsNullOrEmpty(token))
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
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

            var orgId = orgGuid.ToString();
            var sessionId = context.Request.Query["deviceId"].FirstOrDefault() ?? Guid.NewGuid().ToString();

            var liveService = context.RequestServices.GetRequiredService<ILiveMonitoringService>();

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            _logger.LogInformation("[Monitor WS] Admin conectado | Org={OrgId} | Session={SessionId}", orgId, sessionId);

            liveService.RegisterAdmin(orgId, sessionId, socket);

            // Push current state immediately on connect
            var current = liveService.GetAllLiveDevices();
            await SendAsync(socket, new { eventType = "update", payload = current });

            var buffer = new byte[512];
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    if (result.MessageType != WebSocketMessageType.Text) continue;

                    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    try
                    {
                        var msg = JsonSerializer.Deserialize<ViewerCommandDto>(text, _json);
                        if (msg?.Type == null || string.IsNullOrWhiteSpace(msg.DeviceId)) continue;

                        switch (msg.Type.ToLowerInvariant())
                        {
                            case "watch":
                                liveService.AddWatcher(msg.DeviceId, sessionId);
                                break;
                            case "unwatch":
                                liveService.RemoveWatcher(msg.DeviceId, sessionId);
                                break;
                        }
                    }
                    catch (JsonException)
                    {
                        // ping/pong ou mensagem sem formato esperado — ignora
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Monitor WS] Erro | Org={OrgId}", orgId);
            }
            finally
            {
                liveService.UnregisterAdmin(orgId, sessionId);
                liveService.RemoveWatcherFromAllDevices(sessionId);
                if (socket.State == WebSocketState.Open)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Encerrado", CancellationToken.None);
                _logger.LogInformation("[Monitor WS] Admin desconectado | Org={OrgId}", orgId);
            }
        }

        public static async Task SendAsync(WebSocket socket, object payload)
        {
            if (socket.State != WebSocketState.Open) return;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, _json);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private class ViewerCommandDto
        {
            public string? Type { get; set; }
            public string? DeviceId { get; set; }
        }
    }
}
