using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WiseMonitor.Api.Services;
using Xunit;

namespace WiseMonitor.Api.Tests;

/// <summary>
/// Cobre o repasse de frames da tela ao vivo: só quem está assistindo recebe, o
/// agent é avisado quando começar/parar, e o envelope binário sai no formato que o
/// frontend espera ([4 bytes BE][cabeçalho JSON][JPEG]).
/// </summary>
public class LiveFrameRelayTests
{
    private const string DeviceId = "device-abc";
    private const string OrgId = "11111111-1111-1111-1111-111111111111";

    /// <summary>WebSocket falso que só guarda o que foi enviado.</summary>
    private sealed class FakeWebSocket : WebSocket
    {
        public ConcurrentQueue<(byte[] Data, WebSocketMessageType Type)> Sent { get; } = new();

        public override WebSocketState State { get; } = WebSocketState.Open;

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType,
            bool endOfMessage, CancellationToken cancellationToken)
        {
            Sent.Enqueue((buffer.ToArray(), messageType));
            return Task.CompletedTask;
        }

        public override ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType,
            bool endOfMessage, CancellationToken cancellationToken)
        {
            Sent.Enqueue((buffer.ToArray(), messageType));
            return ValueTask.CompletedTask;
        }

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;
        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus s, string? d, CancellationToken c) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus s, string? d, CancellationToken c) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> b, CancellationToken c)
            => Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Binary, true));
    }

    private static async Task<(byte[] Data, WebSocketMessageType Type)> WaitForSendAsync(FakeWebSocket socket)
    {
        // O relay dispara o envio sem aguardar (para não deixar um espectador lento
        // segurar os outros), então o teste precisa esperar o efeito.
        for (var i = 0; i < 100; i++)
        {
            if (socket.Sent.TryDequeue(out var sent)) return sent;
            await Task.Delay(10);
        }

        throw new TimeoutException("Nenhum envio chegou ao socket.");
    }

    [Fact]
    public void Primeiro_espectador_dispara_watch_start_e_ultimo_dispara_watch_stop()
    {
        var live = new LiveMonitoringService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveMonitoringService>.Instance);
        var eventos = new List<bool>();
        live.WatchStateChanged += (_, watching) => eventos.Add(watching);

        live.AddWatcher(DeviceId, "sessao-1");
        live.AddWatcher(DeviceId, "sessao-2"); // segundo não redispara

        live.RemoveWatcher(DeviceId, "sessao-1"); // ainda tem gente olhando
        live.RemoveWatcher(DeviceId, "sessao-2"); // agora sim

        Assert.Equal(new[] { true, false }, eventos);
    }

    [Fact]
    public void Sair_da_pagina_libera_o_device_de_todas_as_sessoes()
    {
        var live = new LiveMonitoringService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveMonitoringService>.Instance);
        var eventos = new List<bool>();
        live.WatchStateChanged += (_, watching) => eventos.Add(watching);

        live.AddWatcher(DeviceId, "sessao-1");
        live.RemoveWatcherFromAllDevices("sessao-1");

        Assert.Equal(new[] { true, false }, eventos);
        Assert.False(live.IsWatched(DeviceId));
    }

    [Fact]
    public void GetWatcherSockets_devolve_so_quem_esta_assistindo_o_device()
    {
        var live = new LiveMonitoringService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveMonitoringService>.Instance);

        var assistindo = new FakeWebSocket();
        var apenasConectado = new FakeWebSocket();

        live.RegisterAdmin(OrgId, "sessao-assiste", assistindo);
        live.RegisterAdmin(OrgId, "sessao-ociosa", apenasConectado);
        live.AddWatcher(DeviceId, "sessao-assiste");

        var sockets = live.GetWatcherSockets(DeviceId);

        Assert.Single(sockets);
        Assert.Same(assistindo, sockets[0]);
    }

    [Fact]
    public async Task Frame_chega_ao_espectador_no_formato_esperado_pelo_frontend()
    {
        var live = new LiveMonitoringService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveMonitoringService>.Instance);
        var relay = new LiveFrameRelay(live, Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveFrameRelay>.Instance);

        var viewer = new FakeWebSocket();
        live.RegisterAdmin(OrgId, "sessao-1", viewer);
        live.AddWatcher(DeviceId, "sessao-1");

        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 0xFF, 0xD9 };
        await relay.RelayFrameAsync(DeviceId, OrgId, new ArraySegment<byte>(jpeg));

        var (data, type) = await WaitForSendAsync(viewer);

        Assert.Equal(WebSocketMessageType.Binary, type);

        // Mesma decodificação que o frontend faz.
        var headerLength = BinaryPrimitives.ReadInt32BigEndian(data);
        var headerJson = Encoding.UTF8.GetString(data, 4, headerLength);
        var header = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(headerJson)!;

        Assert.Equal(DeviceId, header["deviceId"].GetString());

        var payload = data.Skip(4 + headerLength).ToArray();
        Assert.Equal(jpeg, payload);
    }

    [Fact]
    public async Task Frame_nao_vaza_para_admin_que_nao_esta_assistindo()
    {
        var live = new LiveMonitoringService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveMonitoringService>.Instance);
        var relay = new LiveFrameRelay(live, Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveFrameRelay>.Instance);

        var espectador = new FakeWebSocket();
        var xereta = new FakeWebSocket();

        live.RegisterAdmin(OrgId, "sessao-assiste", espectador);
        live.RegisterAdmin(OrgId, "sessao-nao-assiste", xereta);
        live.AddWatcher(DeviceId, "sessao-assiste");

        await relay.RelayFrameAsync(DeviceId, OrgId, new ArraySegment<byte>(new byte[] { 1, 2, 3 }));
        await WaitForSendAsync(espectador);

        Assert.Empty(xereta.Sent);
    }

    [Fact]
    public async Task Espectador_com_teto_de_fps_recebe_menos_frames_que_o_sem_teto()
    {
        var live = new LiveMonitoringService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveMonitoringService>.Instance);
        var relay = new LiveFrameRelay(live, Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveFrameRelay>.Instance);

        var grade = new FakeWebSocket();     // tela de grade: pede 2 fps
        var detalhe = new FakeWebSocket();   // página do device: sem teto

        live.RegisterAdmin(OrgId, "sessao-grade", grade);
        live.RegisterAdmin(OrgId, "sessao-detalhe", detalhe);
        live.AddWatcher(DeviceId, "sessao-grade", maxFps: 2);
        live.AddWatcher(DeviceId, "sessao-detalhe");

        // Rajada de 5 frames de uma vez, bem mais rápida que 2 fps. Sem pausas entre eles:
        // com o suíte rodando em paralelo, um Delay curto podia passar de 500ms e deixar
        // o segundo frame passar legitimamente.
        for (var i = 0; i < 5; i++)
            await relay.RelayFrameAsync(DeviceId, OrgId, new ArraySegment<byte>(new byte[] { 1, 2, 3 }));
        await Task.Delay(50);

        Assert.Single(grade.Sent);
        Assert.Equal(5, detalhe.Sent.Count);
    }

    [Fact]
    public async Task Sem_espectador_nenhum_frame_e_transmitido()
    {
        var live = new LiveMonitoringService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveMonitoringService>.Instance);
        var relay = new LiveFrameRelay(live, Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveFrameRelay>.Instance);

        var admin = new FakeWebSocket();
        live.RegisterAdmin(OrgId, "sessao-1", admin);
        // sem AddWatcher

        await relay.RelayFrameAsync(DeviceId, OrgId, new ArraySegment<byte>(new byte[] { 1, 2, 3 }));
        await Task.Delay(50);

        Assert.Empty(admin.Sent);
    }

    [Fact]
    public async Task Agent_recebe_watch_start_ao_conectar_com_a_pagina_ja_aberta()
    {
        var live = new LiveMonitoringService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveMonitoringService>.Instance);
        var relay = new LiveFrameRelay(live, Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveFrameRelay>.Instance);

        // Espectador chega ANTES do agent conectar — o evento de watch já passou.
        live.RegisterAdmin(OrgId, "sessao-1", new FakeWebSocket());
        live.AddWatcher(DeviceId, "sessao-1");

        var agent = new FakeWebSocket();
        relay.RegisterAgent(DeviceId, OrgId, agent);

        var (data, type) = await WaitForSendAsync(agent);

        Assert.Equal(WebSocketMessageType.Text, type);
        Assert.Contains("watch-start", Encoding.UTF8.GetString(data));
    }

    [Fact]
    public async Task Agent_recebe_watch_stop_quando_o_ultimo_espectador_sai()
    {
        var live = new LiveMonitoringService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveMonitoringService>.Instance);
        var relay = new LiveFrameRelay(live, Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveFrameRelay>.Instance);

        var agent = new FakeWebSocket();
        relay.RegisterAgent(DeviceId, OrgId, agent);

        live.RegisterAdmin(OrgId, "sessao-1", new FakeWebSocket());
        live.AddWatcher(DeviceId, "sessao-1");
        await WaitForSendAsync(agent); // consome o watch-start

        live.RemoveWatcher(DeviceId, "sessao-1");

        var (data, _) = await WaitForSendAsync(agent);
        Assert.Contains("watch-stop", Encoding.UTF8.GetString(data));
    }

    [Fact]
    public async Task Reconexao_do_agent_nao_derruba_o_registro_novo()
    {
        var live = new LiveMonitoringService(Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveMonitoringService>.Instance);
        var relay = new LiveFrameRelay(live, Microsoft.Extensions.Logging.Abstractions.NullLogger<LiveFrameRelay>.Instance);

        var antigo = new FakeWebSocket();
        var novo = new FakeWebSocket();

        relay.RegisterAgent(DeviceId, OrgId, antigo);
        relay.RegisterAgent(DeviceId, OrgId, novo);

        // O finally da conexão antiga roda depois da nova já ter se registrado.
        relay.UnregisterAgent(DeviceId, antigo);

        Assert.True(relay.IsAgentConnected(DeviceId));

        live.RegisterAdmin(OrgId, "sessao-1", new FakeWebSocket());
        live.AddWatcher(DeviceId, "sessao-1");

        var (data, _) = await WaitForSendAsync(novo);
        Assert.Contains("watch-start", Encoding.UTF8.GetString(data));
    }
}
