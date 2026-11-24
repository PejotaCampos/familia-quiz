using System.Security.Cryptography;
using FamiliaQuiz.Shared;

namespace FamiliaQuiz.Server.Game;

/// <summary>
/// One room and all of its game rules. Plain C#, no SignalR: the hub forwards intents here.
/// Every public member is thread-safe (hub calls arrive concurrently).
/// Rule violations throw <see cref="GameException"/>.
/// </summary>
public sealed class GameRoom
{
    private readonly object _sync = new();
    private readonly List<Player> _players = [];
    private readonly HashSet<string> _usedQuestionIds = [];
    private readonly List<string> _topPlayerIds = [];
    private readonly TimeProvider _time;
    private Round? _round;
    private int _roundNumber;

    public GameRoom(string code, int stepCount, TimeProvider? time = null)
    {
        if (stepCount is < GameLimits.MinSteps or > GameLimits.MaxSteps)
            throw new GameException(ErrorCode.InvalidStepCount);

        Code = code;
        StepCount = stepCount;
        _time = time ?? TimeProvider.System;
        LastActivity = _time.GetUtcNow();
    }

    public string Code { get; }
    public int StepCount { get; }

    public GamePhase Phase { get; private set; } = GamePhase.Lobby;
    public string HostPlayerId { get; private set; } = "";
    public string? KingPlayerId { get; private set; }
    public DateTimeOffset LastActivity { get; private set; }

    public bool HasConnectedPlayers
    {
        get { lock (_sync) return _players.Any(p => p.IsConnected); }
    }

    public IReadOnlyList<string> PlayerIds
    {
        get { lock (_sync) return _players.Select(p => p.Id).ToList(); }
    }

    public int GetPosition(string playerId)
    {
        lock (_sync) return GetPlayer(playerId).Position;
    }

    // ---------------------------------------------------------------- joining

    /// <summary>
    /// Adds a player. If a <b>disconnected</b> player with the same name exists, that player is
    /// returned instead, so a relative who lost their browser session can simply join again.
    /// The first player to join becomes the host.
    /// </summary>
    public Player Join(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length == 0 || name.Length > GameLimits.MaxNameLength)
            throw new GameException(ErrorCode.InvalidName);

        lock (_sync)
        {
            Touch();
            var existing = _players.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                if (existing.IsConnected) throw new GameException(ErrorCode.NameTaken);
                return existing;
            }

            if (Phase == GamePhase.Ended) throw new GameException(ErrorCode.WrongPhase);
            if (_players.Count >= GameLimits.MaxPlayers) throw new GameException(ErrorCode.RoomFull);

            var player = new Player(NewId(8), NewId(24), name);
            _players.Add(player);
            if (_players.Count == 1) HostPlayerId = player.Id;
            return player;
        }
    }

    public Player? FindByToken(string token)
    {
        lock (_sync) return _players.FirstOrDefault(p => p.Token == token);
    }

    public void SetConnected(string playerId, bool connected)
    {
        lock (_sync)
        {
            Touch();
            GetPlayer(playerId).IsConnected = connected;
        }
    }

    // ---------------------------------------------------------------- host actions

    public void StartGame(string byPlayerId)
    {
        lock (_sync)
        {
            RequireHost(byPlayerId);
            RequirePhase(GamePhase.Lobby);
            if (_players.Count < 2) throw new GameException(ErrorCode.NotEnoughPlayers);
            Phase = GamePhase.ChoosingKing;
            Touch();
        }
    }

    /// <summary>
    /// Host picks the first King, or replaces the King mid-game (e.g. the King left the call).
    /// Replacing the King cancels the current question but keeps everyone's stair positions.
    /// </summary>
    public void ChooseKing(string byPlayerId, string kingPlayerId)
    {
        lock (_sync)
        {
            RequireHost(byPlayerId);
            if (Phase is GamePhase.Lobby or GamePhase.Ended) throw new GameException(ErrorCode.WrongPhase);
            var king = _players.FirstOrDefault(p => p.Id == kingPlayerId)
                       ?? throw new GameException(ErrorCode.PlayerNotFound);

            king.Position = 0; // the King is off the stairs; when dethroned they start at the bottom
            KingPlayerId = king.Id;
            StartPicking();
        }
    }

    public void EndGame(string byPlayerId)
    {
        lock (_sync)
        {
            RequireHost(byPlayerId);
            Phase = GamePhase.Ended;
            _round = null;
            Touch();
        }
    }

    // ---------------------------------------------------------------- King actions

    /// <summary>The King locks in a question (from the bank, edited, or new) and their secret answer.</summary>
    public void SubmitQuestion(string byPlayerId, string text, IReadOnlyList<string> options, int correctOptionIndex, string? sourceQuestionId)
    {
        text = (text ?? "").Trim();
        var cleanOptions = (options ?? []).Select(o => (o ?? "").Trim()).ToList();

        if (text.Length == 0 || text.Length > GameLimits.MaxQuestionLength
            || cleanOptions.Count is < GameLimits.MinOptions or > GameLimits.MaxOptions
            || cleanOptions.Any(o => o.Length == 0 || o.Length > GameLimits.MaxOptionLength)
            || cleanOptions.Distinct(StringComparer.OrdinalIgnoreCase).Count() != cleanOptions.Count)
            throw new GameException(ErrorCode.InvalidQuestion);

        if (correctOptionIndex < 0 || correctOptionIndex >= cleanOptions.Count)
            throw new GameException(ErrorCode.InvalidOption);

        lock (_sync)
        {
            RequireKing(byPlayerId);
            RequirePhase(GamePhase.KingPicking);

            _roundNumber++;
            _round = new Round(_roundNumber, text, cleanOptions, correctOptionIndex, sourceQuestionId);
            Phase = GamePhase.Guessing;
            Touch();
        }
    }

    public void Reveal(string byPlayerId)
    {
        lock (_sync)
        {
            RequireKing(byPlayerId);
            RequirePhase(GamePhase.Guessing);
            RevealInternal();
        }
    }

    /// <summary>The King can always skip: the question is dropped and nobody moves.</summary>
    public void SkipQuestion(string byPlayerId)
    {
        lock (_sync)
        {
            RequireKing(byPlayerId);
            RequirePhase(GamePhase.Guessing);
            StartPicking();
        }
    }

    /// <summary>
    /// After the reveal: start the next question, or crown the player who reached the top.
    /// When several players reached the top together, the King must pick one via <paramref name="nextKingPlayerId"/>.
    /// </summary>
    public void ContinueAfterReveal(string byPlayerId, string? nextKingPlayerId)
    {
        lock (_sync)
        {
            RequireKing(byPlayerId);
            RequirePhase(GamePhase.Reveal);

            string? newKing = _topPlayerIds.Count switch
            {
                0 => null,
                1 => _topPlayerIds[0],
                _ => nextKingPlayerId is not null && _topPlayerIds.Contains(nextKingPlayerId)
                    ? nextKingPlayerId
                    : throw new GameException(ErrorCode.InvalidNextKing),
            };

            if (newKing is not null)
            {
                KingPlayerId = newKing;
                // v1 rule: when a new King is crowned, everyone restarts at the bottom.
                foreach (var p in _players) p.Position = 0;
            }

            StartPicking();
        }
    }

    // ---------------------------------------------------------------- guesser actions

    /// <summary>
    /// Records (or changes) a guess. When every connected guesser has guessed, the answer is
    /// revealed automatically. Returns true if this guess triggered the reveal.
    /// </summary>
    public bool SubmitGuess(string byPlayerId, int optionIndex)
    {
        lock (_sync)
        {
            var player = GetPlayer(byPlayerId);
            RequirePhase(GamePhase.Guessing);
            if (player.Id == KingPlayerId) throw new GameException(ErrorCode.KingCannotGuess);
            var round = _round!;
            if (optionIndex < 0 || optionIndex >= round.Options.Count) throw new GameException(ErrorCode.InvalidOption);

            round.Guesses[player.Id] = optionIndex;
            Touch();

            var everyoneGuessed = _players
                .Where(p => p.Id != KingPlayerId && p.IsConnected)
                .All(p => round.Guesses.ContainsKey(p.Id));

            if (!everyoneGuessed) return false;
            RevealInternal();
            return true;
        }
    }

    // ---------------------------------------------------------------- state

    /// <summary>Builds the room state as <paramref name="viewerPlayerId"/> is allowed to see it.</summary>
    public RoomStateDto GetStateFor(string viewerPlayerId)
    {
        lock (_sync)
        {
            var isKing = viewerPlayerId == KingPlayerId;
            var round = _round;
            var showAnswer = round is not null && (Phase == GamePhase.Reveal || (isKing && Phase == GamePhase.Guessing));

            var state = new RoomStateDto
            {
                RoomCode = Code,
                StepCount = StepCount,
                Phase = Phase,
                HostPlayerId = HostPlayerId,
                KingPlayerId = KingPlayerId,
                MyPlayerId = viewerPlayerId,
                Players = _players.Select(p => new PlayerDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Position = p.Position,
                    IsConnected = p.IsConnected,
                    IsHost = p.Id == HostPlayerId,
                    IsKing = p.Id == KingPlayerId,
                    HasGuessed = round is not null && round.Guesses.ContainsKey(p.Id),
                }).ToList(),
                UsedQuestionIds = _usedQuestionIds.ToList(),
            };

            if (round is not null && Phase is GamePhase.Guessing or GamePhase.Reveal)
            {
                state.Round = new RoundDto { Number = round.Number, Text = round.Text, Options = round.Options.ToList() };
                state.MyGuess = round.Guesses.TryGetValue(viewerPlayerId, out var g) ? g : null;
                if (showAnswer) state.CorrectOptionIndex = round.CorrectOptionIndex;

                if (Phase == GamePhase.Reveal)
                {
                    state.Guesses = round.Guesses.Select(kv => new GuessDto
                    {
                        PlayerId = kv.Key,
                        OptionIndex = kv.Value,
                        IsCorrect = kv.Value == round.CorrectOptionIndex,
                    }).ToList();
                    state.TopPlayerIds = _topPlayerIds.ToList();
                }
            }

            return state;
        }
    }

    // ---------------------------------------------------------------- internals

    private void RevealInternal()
    {
        var round = _round!;
        foreach (var (playerId, optionIndex) in round.Guesses)
        {
            if (optionIndex != round.CorrectOptionIndex) continue;
            var player = _players.FirstOrDefault(p => p.Id == playerId);
            if (player is not null && player.Id != KingPlayerId)
                player.Position = Math.Min(StepCount, player.Position + 1);
        }

        _topPlayerIds.Clear();
        _topPlayerIds.AddRange(_players.Where(p => p.Id != KingPlayerId && p.Position >= StepCount).Select(p => p.Id));

        if (round.SourceQuestionId is not null) _usedQuestionIds.Add(round.SourceQuestionId);
        Phase = GamePhase.Reveal;
        Touch();
    }

    private void StartPicking()
    {
        _round = null;
        _topPlayerIds.Clear();
        Phase = GamePhase.KingPicking;
        Touch();
    }

    private Player GetPlayer(string playerId) =>
        _players.FirstOrDefault(p => p.Id == playerId) ?? throw new GameException(ErrorCode.NotInRoom);

    private void RequireHost(string playerId)
    {
        GetPlayer(playerId);
        if (playerId != HostPlayerId) throw new GameException(ErrorCode.NotHost);
    }

    private void RequireKing(string playerId)
    {
        GetPlayer(playerId);
        if (playerId != KingPlayerId) throw new GameException(ErrorCode.NotKing);
    }

    private void RequirePhase(GamePhase phase)
    {
        if (Phase != phase) throw new GameException(ErrorCode.WrongPhase);
    }

    private void Touch() => LastActivity = _time.GetUtcNow();

    private static string NewId(int length) =>
        RandomNumberGenerator.GetString("abcdefghijkmnpqrstuvwxyz23456789", length);

    private sealed class Round(int number, string text, List<string> options, int correctOptionIndex, string? sourceQuestionId)
    {
        public int Number { get; } = number;
        public string Text { get; } = text;
        public List<string> Options { get; } = options;
        public int CorrectOptionIndex { get; } = correctOptionIndex;
        public string? SourceQuestionId { get; } = sourceQuestionId;
        public Dictionary<string, int> Guesses { get; } = [];
    }
}
