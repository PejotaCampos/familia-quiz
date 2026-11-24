namespace FamiliaQuiz.Server.Game;

public sealed class Player
{
    internal Player(string id, string token, string name)
    {
        Id = id;
        Token = token;
        Name = name;
    }

    /// <summary>Public id, visible to everyone in the room.</summary>
    public string Id { get; }

    /// <summary>Secret reconnect token, only known by this player's device.</summary>
    public string Token { get; }

    public string Name { get; }

    /// <summary>Step on the stairs: 0 = bottom, StepCount = top.</summary>
    public int Position { get; internal set; }

    public bool IsConnected { get; internal set; }
}
