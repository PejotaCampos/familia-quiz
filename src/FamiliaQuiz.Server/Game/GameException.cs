using FamiliaQuiz.Shared;

namespace FamiliaQuiz.Server.Game;

/// <summary>Thrown when an intent breaks a game rule. The hub turns it into an error result.</summary>
public sealed class GameException(ErrorCode code) : Exception(code.ToString())
{
    public ErrorCode Code { get; } = code;
}
