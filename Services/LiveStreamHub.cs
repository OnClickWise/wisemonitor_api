using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WiseMonitor.Api.DTOs;

public enum LiveRole
{
    Producer,
    Viewer
}

public record LiveClient(
    WebSocket Socket,
    LiveRole Role,
    Guid OrganizationId,
    string SessionId,
    string UserId)
{
    public bool HasSentHello { get; set; }
    public DateTime LastPing { get; set; } = DateTime.UtcNow;

    // 🔑 Chave única da sala
    public string SessionKey => $"{OrganizationId}:{SessionId}";
}

public class LiveStreamHub
{
    // SessionKey -> (ClientId -> Client)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, LiveClient>> _rooms = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ============================================================
    // ➕ ADD CLIENT
    // ============================================================
    public Guid AddClient(LiveClient client)
    {
        var clientId = Guid.NewGuid();

        var room = _rooms.GetOrAdd(
            client.SessionKey,
            _ => new ConcurrentDictionary<Guid, LiveClient>()
        );

        room[clientId] = client;

        Log($"🟢 ADD | {clientId} | {client.SessionKey} | {client.Role}");
        return clientId;
    }

    // ============================================================
    // ➖ REMOVE CLIENT
    // ============================================================
    public void RemoveClient(string sessionKey, Guid clientId)
    {
        if (!_rooms.TryGetValue(sessionKey, out var room))
            return;

        if (room.TryRemove(clientId, out var removed))
            Log($"🔴 REMOVE | {clientId} | {sessionKey} | {removed.Role}");

        if (room.IsEmpty)
        {
            _rooms.TryRemove(sessionKey, out _);
            Log($"🧹 SALA REMOVIDA | {sessionKey}");
        }
    }

    // ============================================================
    // 📡 WEBRTC SIGNAL ROUTING
    // ============================================================
    public async Task BroadcastControlAsync(
        string sessionKey,
        WebRtcSignalDTO signal,
        Guid senderId,
        CancellationToken ct = default)
    {
        if (!_rooms.TryGetValue(sessionKey, out var room))
            return;

        foreach (var (id, client) in room)
        {
            if (id == senderId) continue;
            if (!ShouldForward(signal.Type, client.Role)) continue;
            if (client.Socket.State != WebSocketState.Open) continue;

            await SafeSendAsync(client.Socket, signal, ct);
        }
    }

    // ============================================================
    // 🫀 PING
    // ============================================================
    public void UpdatePing(string sessionKey, Guid clientId)
    {
        if (_rooms.TryGetValue(sessionKey, out var room) &&
            room.TryGetValue(clientId, out var client))
        {
            client.LastPing = DateTime.UtcNow;
        }
    }

    // ============================================================
    // 📊 SNAPSHOT POR ORGANIZAÇÃO
    // ============================================================
    public IEnumerable<object> GetSessionsForOrg(Guid organizationId)
    {
        return _rooms
            .Where(r => r.Key.StartsWith($"{organizationId}:"))
            .Select(r =>
            {
                var clients = r.Value.Values.ToList();

                return new
                {
                    sessionKey = r.Key,
                    total = clients.Count,
                    producers = clients.Count(c => c.Role == LiveRole.Producer),
                    viewers = clients.Count(c => c.Role == LiveRole.Viewer),
                    clients = clients.Select(c => new
                    {
                        c.UserId,
                        role = c.Role.ToString(),
                        c.LastPing
                    })
                };
            });
    }

    // ============================================================
    // 🧠 HELPERS
    // ============================================================
    private static bool ShouldForward(string? type, LiveRole role) =>
        type?.ToLowerInvariant() switch
        {
            "offer" => role == LiveRole.Viewer,
            "answer" => role == LiveRole.Producer,
            "candidate" or "ice-candidate" => true,
            "viewer-join" => role == LiveRole.Producer,
            "viewer-left" => role == LiveRole.Producer,
            _ => false
        };

    private static async Task SafeSendAsync(
        WebSocket socket,
        object payload,
        CancellationToken ct)
    {
        try
        {
            if (socket.State != WebSocketState.Open)
                return;

            var data = JsonSerializer.SerializeToUtf8Bytes(payload, _jsonOptions);
            await socket.SendAsync(data, WebSocketMessageType.Text, true, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Hub] ❌ Send error: {ex.Message}");
        }
    }

    private static void Log(string msg)
        => Console.WriteLine($"[Hub] {DateTime.UtcNow:HH:mm:ss} | {msg}");
}
