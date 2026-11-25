namespace FamiliaQuiz.Shared
{
    public enum GamePhase
    {
        /// <summary>Players are joining; the host can start the game.</summary>
        Lobby = 0,

        /// <summary>The host is choosing the first King.</summary>
        ChoosingKing = 1,

        /// <summary>The King is picking or writing a question and secretly choosing the answer.</summary>
        KingPicking = 2,

        /// <summary>Everyone except the King is guessing.</summary>
        Guessing = 3,

        /// <summary>The correct answer is shown and the stairs have been updated.</summary>
        Reveal = 4,

        /// <summary>The host ended the game.</summary>
        Ended = 5,

        /// <summary>
        /// Special round: the server picked a themed question with fixed (picture) options;
        /// the King secretly chooses one of them. Then it continues as Guessing/Reveal.
        /// </summary>
        SpecialPicking = 6,
    }

    public enum RoundKind
    {
        /// <summary>A question chosen or written by the King.</summary>
        Normal = 0,

        /// <summary>A themed special round with fixed picture options.</summary>
        Special = 1,
    }

    /// <summary>
    /// Machine-readable error codes. Clients map these to their own localized messages.
    /// </summary>
    public enum ErrorCode
    {
        None = 0,
        Unknown,
        RoomNotFound,
        RoomFull,
        InvalidName,
        NameTaken,
        InvalidStepCount,
        InvalidSession,
        NotInRoom,
        NotHost,
        NotKing,
        KingCannotGuess,
        WrongPhase,
        NotEnoughPlayers,
        PlayerNotFound,
        InvalidQuestion,
        InvalidOption,
        InvalidNextKing,
    }
}
