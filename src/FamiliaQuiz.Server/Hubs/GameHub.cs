using FamiliaQuiz.Server.Game;
using FamiliaQuiz.Server.Questions;
using FamiliaQuiz.Server.Rooms;
using FamiliaQuiz.Shared;
using Microsoft.AspNetCore.SignalR;

namespace FamiliaQuiz.Server.Hubs;

/// <summary>
/// The game's public API (used by the Blazor client today, a Unity client later).
/// Thin layer: maps connections to players, forwards intents to <see cref="GameRoom"/>,
/// and pushes each player their own view of the room state.
/// </summary>
public sealed class GameHub(RoomRegistry rooms, QuestionBank questions, ILogger<GameHub> logger) : Hub<IGameClient>
{
    // ---------------------------------------------------------------- joining

    [HubMethodName(HubMethods.CreateRoom)]
    public async Task<JoinRoomResult> CreateRoom(CreateRoomRequest request)
    {
        try
        {
            var room = rooms.CreateRoom(request.StepCount);
            var player = room.Join(request.PlayerName);
            logger.LogInformation("Room {Room} created", room.Code);
            return await EnterRoom(room, player);
        }
        catch (GameException ex)
        {
            return JoinRoomResult.Fail(ex.Code);
        }
    }

    [HubMethodName(HubMethods.JoinRoom)]
    public async Task<JoinRoomResult> JoinRoom(JoinRoomRequest request)
    {
        var room = rooms.FindRoom(request.RoomCode);
        if (room is null) return JoinRoomResult.Fail(ErrorCode.RoomNotFound);
        try
        {
            return await EnterRoom(room, room.Join(request.PlayerName));
        }
        catch (GameException ex)
        {
            return JoinRoomResult.Fail(ex.Code);
        }
    }

    [HubMethodName(HubMethods.Rejoin)]
    public async Task<JoinRoomResult> Rejoin(RejoinRequest request)
    {
        var room = rooms.FindRoom(request.RoomCode);
        if (room is null) return JoinRoomResult.Fail(ErrorCode.RoomNotFound);
        var player = room.FindByToken(request.PlayerToken);
        if (player is null) return JoinRoomResult.Fail(ErrorCode.InvalidSession);
        return await EnterRoom(room, player);
    }

    [HubMethodName(HubMethods.GetQuestionBank)]
    public IReadOnlyList<QuestionDto> GetQuestionBank() => questions.All;

    // ---------------------------------------------------------------- game intents

    [HubMethodName(HubMethods.StartGame)]
    public Task<OperationResult> StartGame() => Act((room, me) => room.StartGame(me));

    [HubMethodName(HubMethods.ChooseKing)]
    public Task<OperationResult> ChooseKing(string playerId) => Act((room, me) => room.ChooseKing(me, playerId));

    [HubMethodName(HubMethods.SubmitQuestion)]
    public Task<OperationResult> SubmitQuestion(SubmitQuestionRequest request) =>
        Act((room, me) => room.SubmitQuestion(me, request.Text, request.Options, request.CorrectOptionIndex, request.SourceQuestionId));

    [HubMethodName(HubMethods.SubmitSpecialAnswer)]
    public Task<OperationResult> SubmitSpecialAnswer(int optionIndex) =>
        Act((room, me) => room.SubmitSpecialAnswer(me, optionIndex));

    [HubMethodName(HubMethods.SubmitGuess)]
    public Task<OperationResult> SubmitGuess(int optionIndex) => Act((room, me) => room.SubmitGuess(me, optionIndex));

    [HubMethodName(HubMethods.Reveal)]
    public Task<OperationResult> Reveal() => Act((room, me) => room.Reveal(me));

    [HubMethodName(HubMethods.SkipQuestion)]
    public Task<OperationResult> SkipQuestion() => Act((room, me) => room.SkipQuestion(me));

    [HubMethodName(HubMethods.ContinueAfterReveal)]
    public Task<OperationResult> ContinueAfterReveal(string? nextKingPlayerId) =>
        Act((room, me) => room.ContinueAfterReveal(me, nextKingPlayerId));

    [HubMethodName(HubMethods.EndGame)]
    public Task<OperationResult> EndGame() => Act((room, me) => room.EndGame(me));

    // ---------------------------------------------------------------- connection lifecycle

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await LeaveCurrentPlayer();
        await base.OnDisconnectedAsync(exception);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<JoinRoomResult> EnterRoom(GameRoom room, Player player)
    {
        await LeaveCurrentPlayer(); // this connection may have been someone else before

        rooms.Attach(Context.ConnectionId, room.Code, player.Id);
        await Groups.AddToGroupAsync(Context.ConnectionId, PlayerGroup(room.Code, player.Id));
        room.SetConnected(player.Id, true);
        await BroadcastState(room);

        return new JoinRoomResult { Success = true, RoomCode = room.Code, PlayerId = player.Id, PlayerToken = player.Token };
    }

    private async Task LeaveCurrentPlayer()
    {
        var (pc, last) = rooms.Detach(Context.ConnectionId);
        if (pc is null) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, PlayerGroup(pc.RoomCode, pc.PlayerId));
        var room = rooms.FindRoom(pc.RoomCode);
        if (room is null || !last) return;
        room.SetConnected(pc.PlayerId, false);
        await BroadcastState(room);
    }

    private async Task<OperationResult> Act(Action<GameRoom, string> action)
    {
        var pc = rooms.Find(Context.ConnectionId);
        var room = pc is null ? null : rooms.FindRoom(pc.RoomCode);
        if (pc is null || room is null) return OperationResult.Fail(ErrorCode.NotInRoom);

        try
        {
            action(room, pc.PlayerId);
        }
        catch (GameException ex)
        {
            return OperationResult.Fail(ex.Code);
        }

        await BroadcastState(room);
        return OperationResult.Ok();
    }

    /// <summary>Each player gets their own view (the secret answer is only in the King's).</summary>
    private Task BroadcastState(GameRoom room) =>
        Task.WhenAll(room.PlayerIds.Select(id =>
            Clients.Group(PlayerGroup(room.Code, id)).RoomStateChanged(room.GetStateFor(id))));

    private static string PlayerGroup(string roomCode, string playerId) => $"{roomCode}:{playerId}";
}
