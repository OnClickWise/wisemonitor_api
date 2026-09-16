using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WiseMonitor.Api.Services
{
    /// <summary>
    /// Repasse de frames da tela ao vivo, do agent para quem está assistindo, via
    /// /ws/agent-stream. Adicional ao histórico por segmentos MP4
    /// (VideoSegmentsController/VideoSegmentService) — este canal é só para "assistir
    /// agora em tempo real", sem gravação/retenção.
    ///
    /// O frame chega binário por conexão persistente e sai binário só para os sockets
    /// que estão olhando aquele device (via ILiveMonitoringService.GetWatcherSockets).
    /// O agent não fica perguntando "alguém está me assistindo?" por HTTP: o relay
    /// avisa por controle na própria conexão quando começar e quando parar
    /// (ILiveMonitoringService.WatchStateChanged).
    /// </summary>
    public class LiveFrameRelay
    {
        /// <summary>
        /// Envelope binário: [4 bytes BE = tamanho do cabeçalho][cabeçalho JSON][JPEG].
        /// O cabeçalho identifica o device porque o mesmo socket de admin pode receber
        /// frames de vários devices numa tela de grade.
        /// </summary>
        private const int HeaderLengthPrefixBytes = 4;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private sealed class AgentConnection
        {
            public required WebSocket Socket { get; init; }
            public required string OrgId { get; init; }
            public SemaphoreSlim SendLock { get; } = new(1, 1);
        }

        private readonly ConcurrentDictionary<string, AgentConnection> _agents = new();

        /// <summary>
        /// Um lock por socket de espectador. Se o envio anterior ainda não terminou
        /// (rede lenta), o frame novo é descartado em vez de enfileirado — ficar para
        /// trás em tempo real é pior que perder quadro.
        /// </summary>
        private readonly ConditionalWeakTable<WebSocket, SemaphoreSlim> _viewerLocks = new();

        private readonly ILiveMonitoringService _liveService;
        private readonly ILogger<LiveFrameRelay> _logger;

        public LiveFrameRelay(ILiveMonitoringService liveService, ILogger<LiveFrameRelay> logger)
        {
            _liveService = liveService;
            _logger = logger;
            _liveService.WatchStateChanged += OnWatchStateChanged;
        }

        // ================================
        // AGENTS
        // ================================

        public void RegisterAgent(string deviceId, string orgId, WebSocket socket)
        {
            _agents[deviceId] = new AgentConnection { Socket = socket, OrgId = orgId };
            _logger.LogInformation("[FrameRelay] Agent conectado | Device={DeviceId} | Org={OrgId}", deviceId, orgId);

            // Se a página já estava aberta antes do agent conectar, ele precisa saber
            // disso agora — senão ficaria parado esperando um evento que já passou.
            if (_liveService.IsWatched(deviceId))
                _ = SendControlAsync(deviceId, "watch-start");
        }

        public void UnregisterAgent(string deviceId, WebSocket socket)
        {
            // Só remove se ainda for a mesma conexão: uma reconexão do agent pode ter
            // registrado um socket novo antes do finally da conexão antiga rodar.
            if (_agents.TryGetValue(deviceId, out var existing) && ReferenceEquals(existing.Socket, socket))
                _agents.TryRemove(deviceId, out _);

            _logger.LogInformation("[FrameRelay] Agent desconectado | Device={DeviceId}", deviceId);
        }

        public bool IsAgentConnected(string deviceId) => _agents.ContainsKey(deviceId);

        // ================================
        // CONTROLE (backend -> agent)
        // ================================

        private void OnWatchStateChanged(string deviceId, bool hasWatchers)
        {
            _ = SendControlAsync(deviceId, hasWatchers ? "watch-start" : "watch-stop");
        }

        private async Task SendControlAsync(string deviceId, string type)
        {
            if (!_agents.TryGetValue(deviceId, out var agent)) return;
            if (agent.Socket.State != WebSocketState.Open) return;

            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { type }, JsonOptions);

            await agent.SendLock.WaitAsync();
            try
            {
                await agent.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
                _logger.LogDebug("[FrameRelay] {Type} | Device={DeviceId}", type, deviceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[FrameRelay] Falha ao enviar controle '{Type}' | Device={DeviceId}", type, deviceId);
            }
            finally
            {
                agent.SendLock.Release();
            }
        }

        // ================================
        // FRAMES (agent -> espectadores)
        // ================================

        public async Task RelayFrameAsync(string deviceId, string orgId, ArraySegment<byte> jpeg)
        {
            var sockets = _liveService.GetWatcherSockets(deviceId);
            if (sockets.Count == 0) return;

            var envelope = BuildEnvelope(deviceId, jpeg);

            foreach (var socket in sockets)
            {
                if (socket.State != WebSocketState.Open) continue;

                var gate = _viewerLocks.GetValue(socket, _ => new SemaphoreSlim(1, 1));

                // WaitAsync(0): se este espectador ainda está recebendo o frame
                // anterior, pula. Sem isso um cliente lento seguraria todos os outros.
                if (!await gate.WaitAsync(0)) continue;

                _ = SendToViewerAsync(socket, envelope, gate);
            }
        }

        private static async Task SendToViewerAsync(WebSocket socket, ReadOnlyMemory<byte> envelope, SemaphoreSlim gate)
        {
            try
            {
                await socket.SendAsync(envelope, WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch
            {
                // Espectador caiu no meio do envio; a limpeza do registro é feita pelo
                // próprio middleware do socket ao encerrar.
            }
            finally
            {
                gate.Release();
            }
        }

        private static byte[] BuildEnvelope(string deviceId, ArraySegment<byte> jpeg)
        {
            var header = JsonSerializer.SerializeToUtf8Bytes(
                new { deviceId, ts = DateTime.UtcNow }, JsonOptions);

            var envelope = new byte[HeaderLengthPrefixBytes + header.Length + jpeg.Count];

            BinaryPrimitives.WriteInt32BigEndian(envelope, header.Length);
            header.CopyTo(envelope.AsSpan(HeaderLengthPrefixBytes));
            jpeg.AsSpan().CopyTo(envelope.AsSpan(HeaderLengthPrefixBytes + header.Length));

            return envelope;
        }
    }
}
