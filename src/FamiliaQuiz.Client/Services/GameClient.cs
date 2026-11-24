using FamiliaQuiz.Shared;
using Microsoft.AspNetCore.SignalR.Client;

namespace FamiliaQuiz.Client.Services;

public enum ConnectionStatus { Disconnected, Connecting, Connected, Reconnecting }

/// <summary>
/// Client side of the game hub. Holds the latest room state pushed by the server and raises
/// <see cref="Changed"/> whenever something the UI shows has changed. The server is authoritative:
/// this class only sends intents and stores what comes back.
/// </summary>
public sealed class GameClient : IAsyncDisposable
{
    private readonly HubConnection _hub;
    private readonly SessionStore _store;
    private readonly ILogger<GameClient> _logger;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private StoredSession? _session;
    private IReadOnlyList<QuestionDto>? _questionBank;

    public GameClient(IConfiguration config, SessionStore store, ILogger<GameClient> logger)
    {
        _store = store;
        _logger = logger;

        var serverUrl = (config["ServerUrl"] ?? "http://localhost:5075").TrimEnd('/');
        _hub = new HubConnectionBuilder()
            .WithUrl(serverUrl + HubRoutes.Game)
            .WithAutomaticReconnect(new RetryForeverPolicy())
            .Build();

        // Must be at least 2x the server's KeepAliveInterval (15s). Generous for throttled background tabs.
        _hub.ServerTimeout = TimeSpan.FromSeconds(90);

        _hub.On<RoomStateDto>(ClientMethods.RoomStateChanged, state =>
        {
            State = state;
            NotifyChanged();
        });

        _hub.Reconnecting += _ => { NotifyChanged(); return Task.CompletedTask; };
        _hub.Reconnected += async _ =>
        {
            // A new connection id: tell the server who we are again to get the current state back.
            await RejoinAsync();
            NotifyChanged();
        };
        _hub.Closed += async _ =>
        {
            NotifyChanged();
            if (_session is not null) await ConnectAndRejoinAsync();
        };
    }

    public event Action? Changed;

    public bool IsInitialized { get; private set; }
    public RoomStateDto? State { get; private set; }
    public ErrorCode? LastError { get; private set; }

    public ConnectionStatus Status => _hub.State switch
    {
        HubConnectionState.Connected => ConnectionStatus.Connected,
        HubConnectionState.Connecting => ConnectionStatus.Connecting,
        HubConnectionState.Reconnecting => ConnectionStatus.Reconnecting,
        _ => ConnectionStatus.Disconnected,
    };

    /// <summary>Connects right away (wakes a sleeping server) and rejoins a stored session, if any.</summary>
    public async Task InitializeAsync()
    {
        if (IsInitialized) return;
        _session = await _store.LoadAsync();
        IsInitialized = _session is null; // without a session the entry form can show immediately
        NotifyChanged();

        await ConnectAndRejoinAsync();

        IsInitialized = true;
        NotifyChanged();
    }

    // ---------------------------------------------------------------- joining

    public Task<bool> CreateRoomAsync(string name, int stepCount) =>
        EnterAsync(HubMethods.CreateRoom, new CreateRoomRequest { PlayerName = name, StepCount = stepCount }, name);

    public Task<bool> JoinRoomAsync(string roomCode, string name) =>
        EnterAsync(HubMethods.JoinRoom, new JoinRoomRequest { RoomCode = roomCode.Trim().ToUpperInvariant(), PlayerName = name }, name);

    /// <summary>Forget this room on this device (the player stays in the room as "offline").</summary>
    public async Task LeaveAsync()
    {
        _session = null;
        State = null;
        await _store.ClearAsync();
        await _hub.StopAsync(); // drops the server-side link to the player
        NotifyChanged();
        _ = ConnectAndRejoinAsync(); // stay connected for the next join
    }

    public Task<string> GetRememberedNameAsync() => _store.LoadNameAsync();

    public async Task<IReadOnlyList<QuestionDto>> GetQuestionBankAsync()
    {
        if (_questionBank is not null) return _questionBank;
        try
        {
            _questionBank = await _hub.InvokeAsync<List<QuestionDto>>(HubMethods.GetQuestionBank);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load question bank");
            return [];
        }
        return _questionBank;
    }

    // ---------------------------------------------------------------- game intents

    public Task<bool> StartGameAsync() => ActAsync(HubMethods.StartGame);
    public Task<bool> ChooseKingAsync(string playerId) => ActAsync(HubMethods.ChooseKing, playerId);
    public Task<bool> SubmitQuestionAsync(SubmitQuestionRequest request) => ActAsync(HubMethods.SubmitQuestion, request);
    public Task<bool> SubmitGuessAsync(int optionIndex) => ActAsync(HubMethods.SubmitGuess, optionIndex);
    public Task<bool> RevealAsync() => ActAsync(HubMethods.Reveal);
    public Task<bool> SkipQuestionAsync() => ActAsync(HubMethods.SkipQuestion);
    public Task<bool> ContinueAfterRevealAsync(string? nextKingPlayerId) => ActAsync(HubMethods.ContinueAfterReveal, nextKingPlayerId);
    public Task<bool> EndGameAsync() => ActAsync(HubMethods.EndGame);

    public void ClearError()
    {
        LastError = null;
        NotifyChanged();
    }

    // ---------------------------------------------------------------- internals

    private async Task<bool> EnterAsync(string method, object request, string name)
    {
        LastError = null;
        await _store.SaveNameAsync(name.Trim());
        var result = await InvokeAsync<JoinRoomResult>(method, request);
        if (result is null) return false;
        if (!result.Success)
        {
            SetError(result.Error);
            return false;
        }

        _session = new StoredSession(result.RoomCode, result.PlayerToken);
        await _store.SaveAsync(_session);
        return true;
    }

    private async Task<bool> ActAsync(string method, params object?[] args)
    {
        LastError = null;
        var result = await InvokeAsync<OperationResult>(method, args);
        if (result is null) return false;
        if (!result.Success) SetError(result.Error);
        return result.Success;
    }

    private async Task<T?> InvokeAsync<T>(string method, params object?[] args) where T : class
    {
        try
        {
            await EnsureConnectedAsync();
            return await _hub.InvokeCoreAsync<T>(method, args);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hub call {Method} failed", method);
            SetError(ErrorCode.Unknown);
            return null;
        }
    }

    private async Task RejoinAsync()
    {
        if (_session is null) return;
        var result = await _hub.InvokeAsync<JoinRoomResult>(
            HubMethods.Rejoin, new RejoinRequest { RoomCode = _session.RoomCode, PlayerToken = _session.PlayerToken });

        if (!result.Success)
        {
            // Room is gone (server restarted or cleaned up) or the token is unknown: back to the entry screen.
            _logger.LogInformation("Rejoin failed: {Error}", result.Error);
            _session = null;
            State = null;
            await _store.ClearAsync();
            SetError(result.Error);
        }
    }

    /// <summary>Connects (retrying while the free-tier server wakes up) and rejoins the stored session.</summary>
    private async Task ConnectAndRejoinAsync()
    {
        try
        {
            await EnsureConnectedAsync();
            await RejoinAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not connect or rejoin");
        }
        NotifyChanged();
    }

    private async Task EnsureConnectedAsync()
    {
        if (_hub.State == HubConnectionState.Connected) return;
        await _connectLock.WaitAsync();
        try
        {
            var delay = TimeSpan.FromSeconds(1);
            while (_hub.State == HubConnectionState.Disconnected)
            {
                try
                {
                    var start = _hub.StartAsync();
                    NotifyChanged(); // shows "Connecting..."
                    await start;
                }
                catch (Exception ex)
                {
                    _logger.LogInformation("Connect failed ({Message}), retrying in {Delay}", ex.Message, delay);
                    NotifyChanged();
                    await Task.Delay(delay);
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 10));
                }
            }

            // Automatic reconnect may be in progress: wait for it rather than failing the call.
            while (_hub.State is HubConnectionState.Reconnecting or HubConnectionState.Connecting)
                await Task.Delay(250);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private void SetError(ErrorCode error)
    {
        LastError = error;
        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke();

    public async ValueTask DisposeAsync() => await _hub.DisposeAsync();

    /// <summary>Never give up reconnecting: 0s, 1s, 2s, 5s, then every 5s.</summary>
    private sealed class RetryForeverPolicy : IRetryPolicy
    {
        private static readonly TimeSpan[] Delays = [TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];

        public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
            retryContext.PreviousRetryCount < Delays.Length ? Delays[retryContext.PreviousRetryCount] : TimeSpan.FromSeconds(5);
    }
}
