using System.Collections.Generic;

namespace FamiliaQuiz.Shared
{
    /// <summary>Game limits shared by server validation and client forms.</summary>
    public static class GameLimits
    {
        public const int MinSteps = 5;
        public const int MaxSteps = 10;
        public const int DefaultSteps = 5;
        public const int MinOptions = 2;
        public const int MaxOptions = 4;
        public const int MaxPlayers = 20;
        public const int MaxNameLength = 24;
        public const int MaxQuestionLength = 200;
        public const int MaxOptionLength = 80;
    }

    // ---------- Requests ----------

    public class CreateRoomRequest
    {
        public string PlayerName { get; set; } = "";
        public int StepCount { get; set; } = GameLimits.DefaultSteps;
    }

    public class JoinRoomRequest
    {
        public string RoomCode { get; set; } = "";
        public string PlayerName { get; set; } = "";
    }

    public class RejoinRequest
    {
        public string RoomCode { get; set; } = "";
        public string PlayerToken { get; set; } = "";
    }

    public class SubmitQuestionRequest
    {
        public string Text { get; set; } = "";
        public List<string> Options { get; set; } = new List<string>();

        /// <summary>The King's secret answer. Only ever sent from the King to the server.</summary>
        public int CorrectOptionIndex { get; set; }

        /// <summary>Id of the bank question this was based on (null for a brand-new question).</summary>
        public string? SourceQuestionId { get; set; }
    }

    // ---------- Results ----------

    public class OperationResult
    {
        public bool Success { get; set; }
        public ErrorCode Error { get; set; }

        public static OperationResult Ok() => new OperationResult { Success = true };
        public static OperationResult Fail(ErrorCode error) => new OperationResult { Success = false, Error = error };
    }

    public class JoinRoomResult : OperationResult
    {
        public string RoomCode { get; set; } = "";

        /// <summary>Public id of this player (visible to everyone in the room).</summary>
        public string PlayerId { get; set; } = "";

        /// <summary>Secret token used to rejoin as the same player. Store it locally; never show it.</summary>
        public string PlayerToken { get; set; } = "";

        public static new JoinRoomResult Fail(ErrorCode error) => new JoinRoomResult { Success = false, Error = error };
    }

    // ---------- State ----------

    public class QuestionDto
    {
        public string Id { get; set; } = "";
        public string Category { get; set; } = "";
        public string Text { get; set; } = "";
        public List<string> Options { get; set; } = new List<string>();
    }

    public class PlayerDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";

        /// <summary>Step on the stairs: 0 = bottom, StepCount = top.</summary>
        public int Position { get; set; }

        public bool IsConnected { get; set; }
        public bool IsHost { get; set; }
        public bool IsKing { get; set; }

        /// <summary>During Guessing: whether this player already sent a guess (not which one).</summary>
        public bool HasGuessed { get; set; }
    }

    public class RoundOptionDto
    {
        public string Text { get; set; } = "";

        /// <summary>Optional emoji shown when there is no image (or while it loads).</summary>
        public string? Emoji { get; set; }

        /// <summary>
        /// Optional picture, as a path relative to the client's special-round image folder
        /// (e.g. "arma-zumbi/katana.webp"). Clients resolve it to their own asset location.
        /// </summary>
        public string? Image { get; set; }
    }

    public class RoundDto
    {
        public int Number { get; set; }
        public RoundKind Kind { get; set; }

        /// <summary>Theme id for special rounds (null for normal rounds).</summary>
        public string? ThemeId { get; set; }

        public string Text { get; set; } = "";
        public List<RoundOptionDto> Options { get; set; } = new List<RoundOptionDto>();
    }

    public class GuessDto
    {
        public string PlayerId { get; set; } = "";
        public int OptionIndex { get; set; }
        public bool IsCorrect { get; set; }
    }

    /// <summary>
    /// Room state as seen by one specific player. The server builds a separate copy per player,
    /// so secret data (the correct answer) is only included when that player may see it.
    /// </summary>
    public class RoomStateDto
    {
        public string RoomCode { get; set; } = "";
        public int StepCount { get; set; }
        public GamePhase Phase { get; set; }
        public string HostPlayerId { get; set; } = "";
        public string? KingPlayerId { get; set; }

        /// <summary>The player this state was built for.</summary>
        public string MyPlayerId { get; set; } = "";

        public List<PlayerDto> Players { get; set; } = new List<PlayerDto>();

        /// <summary>The question being played (SpecialPicking, Guessing and Reveal phases).</summary>
        public RoundDto? Round { get; set; }

        /// <summary>Only set for the King during Guessing, and for everyone during Reveal.</summary>
        public int? CorrectOptionIndex { get; set; }

        /// <summary>This player's current guess, if any.</summary>
        public int? MyGuess { get; set; }

        /// <summary>Everyone's guesses. Only filled during Reveal.</summary>
        public List<GuessDto> Guesses { get; set; } = new List<GuessDto>();

        /// <summary>
        /// During Reveal: players who reached the top. One = next King.
        /// Several = the current King must choose one of them.
        /// </summary>
        public List<string> TopPlayerIds { get; set; } = new List<string>();

        /// <summary>
        /// The step that triggers this reign's special round (reaching it makes the next round special).
        /// Null when the special round was already played this reign, or none are available.
        /// </summary>
        public int? SpecialStep { get; set; }

        /// <summary>During Reveal: true when the next round will be a special round.</summary>
        public bool NextRoundIsSpecial { get; set; }

        /// <summary>Bank question ids already played in this room.</summary>
        public List<string> UsedQuestionIds { get; set; } = new List<string>();
    }
}
