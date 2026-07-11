/*using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.Services;
using WiseMonitor.Api.Helpers;

namespace WiseMonitor.Api.Middlewares
{
    public class WebSocketMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILiveMonitoringService _liveMonitoring;
        private readonly JwtHelper _jwtHelper;
        private readonly ILogger<WebSocketMiddleware> _logger;

        public WebSocketMiddleware(
            RequestDelegate next,
            ILiveMonitoringService liveMonitoring,
            JwtHelper jwtHelper,
            ILogger<WebSocketMiddleware> logger)
        {
            _next = next;
            _liveMonitoring = liveMonitoring;
            _jwtHelper = jwtHelper;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments("/ws/live"))
            {
                await _next(context);
                return;
            }

            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Requisição não é WebSocket.");
                return;
            }

            string? token = context.Request.Query["token"].FirstOrDefault();
            if (string.IsNullOrEmpty(token))
            {
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                    token = authHeader.Substring("Bearer ".Length);
            }

            string sessionId = context.Request.Query["sessionId"].FirstOrDefault() ?? "";
            string orgId = context.Request.Query["organizationId"].FirstOrDefault() ?? "";

            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(orgId))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Parâmetros 'organizationId' e 'sessionId' são obrigatórios.");
                _logger.LogWarning("[WS] ❌ Falha: parâmetros ausentes | orgId={Org} | sessionId={Session}", orgId, sessionId);
                return;
            }

            string userId = "anon";
            string tenantId = orgId;

            if (!string.IsNullOrEmpty(token) && _jwtHelper.ValidateToken(token, out var claimsPrincipal))
            {
                userId = claimsPrincipal?.Claims.FirstOrDefault(c => c.Type == "id")?.Value ?? "anon";
                tenantId = claimsPrincipal?.Claims.FirstOrDefault(c => c.Type == "tenant")?.Value ?? orgId;
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Token JWT inválido ou ausente.");
                return;
            }

            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
            _logger.LogInformation("[WS] ✅ Admin conectado | User={User} | Tenant={Tenant} | SessionId={Session}", userId, tenantId, sessionId);

            _liveMonitoring.RegisterAdmin(tenantId, sessionId, webSocket);
            var buffer = new byte[1024];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("[WS] 🔴 Admin desconectado | User={User} | Tenant={Tenant} | SessionId={Session}", userId, tenantId, sessionId);
                        break;
                    }
                }
            }
            catch (WebSocketException wsex)
            {
                _logger.LogWarning("WebSocket desconectado abruptamente: {Message}", wsex.Message);
            }
            catch (SocketException sex)
            {
                _logger.LogWarning("Conexão resetada pelo cliente: {Message}", sex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado no WebSocketMiddleware | User={User} | Tenant={Tenant}", userId, tenantId);
                throw;
            }
            finally
            {
                _liveMonitoring.UnregisterAdmin(tenantId, sessionId);
                _logger.LogInformation("[WS] 🧹 Admin removido do LiveMonitoring | User={User} | Tenant={Tenant} | SessionId={Session}", userId, tenantId, sessionId);
            }
        }
    }
}
*/
