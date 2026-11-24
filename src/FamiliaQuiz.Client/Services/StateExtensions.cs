using FamiliaQuiz.Shared;

namespace FamiliaQuiz.Client.Services;

/// <summary>Convenience lookups on the room state for the UI.</summary>
public static class StateExtensions
{
    public static PlayerDto? Me(this RoomStateDto s) => s.Players.FirstOrDefault(p => p.Id == s.MyPlayerId);
    public static PlayerDto? King(this RoomStateDto s) => s.Players.FirstOrDefault(p => p.Id == s.KingPlayerId);
    public static bool AmIHost(this RoomStateDto s) => s.MyPlayerId == s.HostPlayerId;
    public static bool AmIKing(this RoomStateDto s) => s.KingPlayerId is not null && s.MyPlayerId == s.KingPlayerId;
    public static string NameOf(this RoomStateDto s, string playerId) => s.Players.FirstOrDefault(p => p.Id == playerId)?.Name ?? "?";
    public static IEnumerable<PlayerDto> Guessers(this RoomStateDto s) => s.Players.Where(p => p.Id != s.KingPlayerId);
}
