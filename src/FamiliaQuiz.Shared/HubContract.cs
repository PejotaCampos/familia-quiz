using System.Threading.Tasks;

namespace FamiliaQuiz.Shared
{
    /// <summary>Route where the server maps the game hub.</summary>
    public static class HubRoutes
    {
        public const string Game = "/hubs/game";
    }

    /// <summary>
    /// Methods a client can invoke on the hub (client -> server intents).
    /// Every method returns a <see cref="OperationResult"/> or <see cref="JoinRoomResult"/>.
    /// </summary>
    public static class HubMethods
    {
        /// <summary>(CreateRoomRequest) -> JoinRoomResult</summary>
        public const string CreateRoom = "CreateRoom";

        /// <summary>(JoinRoomRequest) -> JoinRoomResult</summary>
        public const string JoinRoom = "JoinRoom";

        /// <summary>(RejoinRequest) -> JoinRoomResult. Call after every (re)connect when a session is stored.</summary>
        public const string Rejoin = "Rejoin";

        /// <summary>() -> List&lt;QuestionDto&gt;</summary>
        public const string GetQuestionBank = "GetQuestionBank";

        /// <summary>Host only. () -> OperationResult</summary>
        public const string StartGame = "StartGame";

        /// <summary>Host only. (string playerId) -> OperationResult. Picks the first King, or replaces the King mid-game.</summary>
        public const string ChooseKing = "ChooseKing";

        /// <summary>King only. (SubmitQuestionRequest) -> OperationResult</summary>
        public const string SubmitQuestion = "SubmitQuestion";

        /// <summary>Guessers only. (int optionIndex) -> OperationResult. Can be changed until the reveal.</summary>
        public const string SubmitGuess = "SubmitGuess";

        /// <summary>King only. () -> OperationResult</summary>
        public const string Reveal = "Reveal";

        /// <summary>King only. () -> OperationResult</summary>
        public const string SkipQuestion = "SkipQuestion";

        /// <summary>King only. (string? nextKingPlayerId) -> OperationResult. The id is required on a tie at the top.</summary>
        public const string ContinueAfterReveal = "ContinueAfterReveal";

        /// <summary>Host only. () -> OperationResult</summary>
        public const string EndGame = "EndGame";
    }

    /// <summary>Messages the server pushes to clients (server -> client).</summary>
    public static class ClientMethods
    {
        /// <summary>(RoomStateDto) Full room state as seen by the receiving player.</summary>
        public const string RoomStateChanged = nameof(IGameClient.RoomStateChanged);
    }

    /// <summary>Strongly-typed shape of the server -> client messages.</summary>
    public interface IGameClient
    {
        Task RoomStateChanged(RoomStateDto state);
    }
}
