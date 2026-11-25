using System.Collections.Concurrent;
using System.Security.Cryptography;
using FamiliaQuiz.Server.Game;
using FamiliaQuiz.Server.Questions;

namespace FamiliaQuiz.Server.Rooms;

/// <summary>
/// In-memory store of rooms and of which SignalR connection belongs to which player.
/// Rooms are lost on restart, which is acceptable (Render free tier has no persistent disk).
/// </summary>
public sealed class RoomRegistry(TimeProvider time, SpecialRoundBank specialRounds)
{
    // No vowels (avoids accidental words) and no look-alikes (0/O, 1/I).
    private const string CodeAlphabet = "BCDFGHJKLMNPQRSTVWXZ";
    private const int CodeLength = 4;

    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _connectionsSync = new();
    private readonly Dictionary<string, PlayerConnection> _connections = [];

    public GameRoom CreateRoom(int stepCount)
    {
        while (true)
        {
            var code = RandomNumberGenerator.GetString(CodeAlphabet, CodeLength);
            var room = new GameRoom(code, stepCount, specialRounds.Themes, time);
            if (_rooms.TryAdd(code, room)) return room;
        }
    }

    public GameRoom? FindRoom(string code) =>
        _rooms.TryGetValue((code ?? "").Trim(), out var room) ? room : null;

    /// <summary>Links a connection to a player (a player may have several, e.g. two tabs).</summary>
    public void Attach(string connectionId, string roomCode, string playerId)
    {
        lock (_connectionsSync) _connections[connectionId] = new PlayerConnection(roomCode, playerId);
    }

    public PlayerConnection? Find(string connectionId)
    {
        lock (_connectionsSync) return _connections.GetValueOrDefault(connectionId);
    }

    /// <summary>
    /// Unlinks a connection. Returns the player it belonged to and whether that player has
    /// no other open connections left (e.g. a second browser tab).
    /// </summary>
    public (PlayerConnection? Player, bool LastConnection) Detach(string connectionId)
    {
        lock (_connectionsSync)
        {
            if (!_connections.Remove(connectionId, out var pc)) return (null, false);
            var stillConnected = _connections.Values.Any(c => c == pc);
            return (pc, !stillConnected);
        }
    }

    /// <summary>Removes rooms where nobody has been connected or active for a while.</summary>
    public int RemoveIdleRooms(TimeSpan maxIdle)
    {
        var now = time.GetUtcNow();
        var removed = 0;
        foreach (var (code, room) in _rooms)
        {
            if (!room.HasConnectedPlayers && now - room.LastActivity > maxIdle && _rooms.TryRemove(code, out _))
                removed++;
        }
        return removed;
    }
}

public sealed record PlayerConnection(string RoomCode, string PlayerId);
