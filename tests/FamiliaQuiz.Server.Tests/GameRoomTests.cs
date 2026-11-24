using FamiliaQuiz.Server.Game;
using FamiliaQuiz.Shared;

namespace FamiliaQuiz.Server.Tests;

public class GameRoomTests
{
    private static readonly string[] Options = ["Azul", "Verde", "Vermelho", "Amarelo"];

    /// <summary>Room in KingPicking with host (King), plus two connected guessers.</summary>
    private static (GameRoom room, Player king, Player ana, Player bia) StartedRoom(int steps = 5)
    {
        var room = new GameRoom("ABCD", steps);
        var king = room.Join("Pedro");
        var ana = room.Join("Ana");
        var bia = room.Join("Bia");
        foreach (var p in new[] { king, ana, bia }) room.SetConnected(p.Id, true);
        room.StartGame(king.Id);
        room.ChooseKing(king.Id, king.Id);
        return (room, king, ana, bia);
    }

    private static void Ask(GameRoom room, Player king, int correct = 0, string? sourceId = null) =>
        room.SubmitQuestion(king.Id, "Qual é a minha cor favorita?", Options, correct, sourceId);

    [Theory]
    [InlineData(4)]
    [InlineData(11)]
    public void StepCount_outside_5_to_10_is_rejected(int steps)
    {
        var ex = Assert.Throws<GameException>(() => new GameRoom("ABCD", steps));
        Assert.Equal(ErrorCode.InvalidStepCount, ex.Code);
    }

    [Fact]
    public void First_player_becomes_host()
    {
        var room = new GameRoom("ABCD", 5);
        var first = room.Join("Pedro");
        room.Join("Ana");
        Assert.Equal(first.Id, room.HostPlayerId);
    }

    [Fact]
    public void Joining_with_name_of_disconnected_player_reclaims_that_player()
    {
        var room = new GameRoom("ABCD", 5);
        var ana = room.Join("Ana");
        room.SetConnected(ana.Id, false);

        var again = room.Join(" ana ");

        Assert.Same(ana, again);
    }

    [Fact]
    public void Joining_with_name_of_connected_player_is_rejected()
    {
        var room = new GameRoom("ABCD", 5);
        var ana = room.Join("Ana");
        room.SetConnected(ana.Id, true);

        var ex = Assert.Throws<GameException>(() => room.Join("ANA"));
        Assert.Equal(ErrorCode.NameTaken, ex.Code);
    }

    [Fact]
    public void Only_host_can_start_and_needs_two_players()
    {
        var room = new GameRoom("ABCD", 5);
        var host = room.Join("Pedro");
        Assert.Equal(ErrorCode.NotEnoughPlayers, Assert.Throws<GameException>(() => room.StartGame(host.Id)).Code);

        var ana = room.Join("Ana");
        Assert.Equal(ErrorCode.NotHost, Assert.Throws<GameException>(() => room.StartGame(ana.Id)).Code);

        room.StartGame(host.Id);
        Assert.Equal(GamePhase.ChoosingKing, room.Phase);
    }

    [Fact]
    public void Guessers_never_see_the_answer_before_reveal_but_the_king_does()
    {
        var (room, king, ana, _) = StartedRoom();
        Ask(room, king, correct: 2);

        Assert.Null(room.GetStateFor(ana.Id).CorrectOptionIndex);
        Assert.Empty(room.GetStateFor(ana.Id).Guesses);
        Assert.Equal(2, room.GetStateFor(king.Id).CorrectOptionIndex);
    }

    [Fact]
    public void Guess_state_shows_who_guessed_but_not_what()
    {
        var (room, king, ana, bia) = StartedRoom();
        Ask(room, king);
        room.SubmitGuess(ana.Id, 1);

        var biaView = room.GetStateFor(bia.Id);
        Assert.True(biaView.Players.Single(p => p.Id == ana.Id).HasGuessed);
        Assert.Null(biaView.MyGuess);
        Assert.Empty(biaView.Guesses);
        Assert.Equal(1, room.GetStateFor(ana.Id).MyGuess);
    }

    [Fact]
    public void King_cannot_guess()
    {
        var (room, king, _, _) = StartedRoom();
        Ask(room, king);
        Assert.Equal(ErrorCode.KingCannotGuess, Assert.Throws<GameException>(() => room.SubmitGuess(king.Id, 0)).Code);
    }

    [Fact]
    public void Reveal_moves_only_correct_guessers_up_one_step()
    {
        var (room, king, ana, bia) = StartedRoom();
        Ask(room, king, correct: 0);
        room.SubmitGuess(ana.Id, 0);
        room.SubmitGuess(bia.Id, 3); // everyone guessed -> auto reveal

        Assert.Equal(GamePhase.Reveal, room.Phase);
        Assert.Equal(1, room.GetPosition(ana.Id));
        Assert.Equal(0, room.GetPosition(bia.Id));
        Assert.Equal(0, room.GetPosition(king.Id));

        var view = room.GetStateFor(bia.Id);
        Assert.Equal(0, view.CorrectOptionIndex);
        Assert.Contains(view.Guesses, g => g.PlayerId == ana.Id && g.IsCorrect);
    }

    [Fact]
    public void Disconnected_guessers_do_not_block_auto_reveal()
    {
        var (room, king, ana, bia) = StartedRoom();
        room.SetConnected(bia.Id, false);
        Ask(room, king);

        var revealed = room.SubmitGuess(ana.Id, 0);

        Assert.True(revealed);
        Assert.Equal(GamePhase.Reveal, room.Phase);
    }

    [Fact]
    public void Guess_can_be_changed_before_reveal()
    {
        var (room, king, ana, _) = StartedRoom();
        Ask(room, king, correct: 1);
        room.SubmitGuess(ana.Id, 0);
        room.SubmitGuess(ana.Id, 1);
        room.Reveal(king.Id);
        Assert.Equal(1, room.GetPosition(ana.Id));
    }

    [Fact]
    public void Skip_drops_the_question_and_nobody_moves()
    {
        var (room, king, ana, _) = StartedRoom();
        Ask(room, king, correct: 0);
        room.SubmitGuess(ana.Id, 0);

        room.SkipQuestion(king.Id);

        Assert.Equal(GamePhase.KingPicking, room.Phase);
        Assert.Equal(0, room.GetPosition(ana.Id));
        Assert.Null(room.GetStateFor(ana.Id).Round);
    }

    [Fact]
    public void Only_king_can_submit_question_reveal_and_skip()
    {
        var (room, king, ana, _) = StartedRoom();
        Assert.Equal(ErrorCode.NotKing, Assert.Throws<GameException>(() => Ask(room, ana)).Code);
        Ask(room, king);
        Assert.Equal(ErrorCode.NotKing, Assert.Throws<GameException>(() => room.Reveal(ana.Id)).Code);
        Assert.Equal(ErrorCode.NotKing, Assert.Throws<GameException>(() => room.SkipQuestion(ana.Id)).Code);
    }

    [Theory]
    [InlineData("", new[] { "a", "b" }, 0)]
    [InlineData("Pergunta?", new[] { "a" }, 0)]
    [InlineData("Pergunta?", new[] { "a", "b", "c", "d", "e" }, 0)]
    [InlineData("Pergunta?", new[] { "a", " " }, 0)]
    [InlineData("Pergunta?", new[] { "a", "A" }, 0)]
    public void Invalid_questions_are_rejected(string text, string[] options, int correct)
    {
        var (room, king, _, _) = StartedRoom();
        var ex = Assert.Throws<GameException>(() => room.SubmitQuestion(king.Id, text, options, correct, null));
        Assert.Equal(ErrorCode.InvalidQuestion, ex.Code);
    }

    [Fact]
    public void Answer_index_out_of_range_is_rejected()
    {
        var (room, king, _, _) = StartedRoom();
        var ex = Assert.Throws<GameException>(() => room.SubmitQuestion(king.Id, "Q?", ["a", "b"], 2, null));
        Assert.Equal(ErrorCode.InvalidOption, ex.Code);
    }

    [Fact]
    public void Reaching_the_top_crowns_the_new_king_and_everyone_restarts()
    {
        var (room, king, ana, bia) = StartedRoom(steps: 5);
        for (var i = 0; i < 5; i++)
        {
            Ask(room, king, correct: 0);
            room.SubmitGuess(ana.Id, 0);
            room.SubmitGuess(bia.Id, i == 4 ? 1 : 0); // Bia misses the last one
            if (i < 4) room.ContinueAfterReveal(king.Id, null);
        }

        Assert.Equal([ana.Id], room.GetStateFor(king.Id).TopPlayerIds);
        Assert.Equal(5, room.GetPosition(ana.Id));

        room.ContinueAfterReveal(king.Id, null);

        Assert.Equal(ana.Id, room.KingPlayerId);
        Assert.Equal(GamePhase.KingPicking, room.Phase);
        Assert.All(room.PlayerIds, id => Assert.Equal(0, room.GetPosition(id)));
    }

    [Fact]
    public void Tie_at_the_top_requires_the_king_to_choose()
    {
        var (room, king, ana, bia) = StartedRoom(steps: 5);
        for (var i = 0; i < 5; i++)
        {
            Ask(room, king, correct: 0);
            room.SubmitGuess(ana.Id, 0);
            room.SubmitGuess(bia.Id, 0);
            if (i < 4) room.ContinueAfterReveal(king.Id, null);
        }

        Assert.Equal(2, room.GetStateFor(king.Id).TopPlayerIds.Count);
        Assert.Equal(ErrorCode.InvalidNextKing, Assert.Throws<GameException>(() => room.ContinueAfterReveal(king.Id, null)).Code);
        Assert.Equal(ErrorCode.InvalidNextKing, Assert.Throws<GameException>(() => room.ContinueAfterReveal(king.Id, king.Id)).Code);

        room.ContinueAfterReveal(king.Id, bia.Id);
        Assert.Equal(bia.Id, room.KingPlayerId);
    }

    [Fact]
    public void Positions_never_exceed_the_top()
    {
        var (room, king, ana, _) = StartedRoom(steps: 5);
        for (var i = 0; i < 5; i++)
        {
            Ask(room, king, correct: 0);
            room.SubmitGuess(ana.Id, 0);
            room.Reveal(king.Id);
            if (i < 4) room.ContinueAfterReveal(king.Id, null);
        }

        // Host replaces the King instead of crowning: positions are kept, Ana stays at the top.
        room.ChooseKing(king.Id, king.Id);
        Ask(room, king, correct: 0);
        room.SubmitGuess(ana.Id, 0);
        room.Reveal(king.Id);

        Assert.Equal(5, room.GetPosition(ana.Id));
    }

    [Fact]
    public void Host_can_replace_the_king_mid_game_and_positions_are_kept()
    {
        var (room, king, ana, bia) = StartedRoom();
        Ask(room, king, correct: 0);
        room.SubmitGuess(ana.Id, 0);
        room.SubmitGuess(bia.Id, 0);
        room.ContinueAfterReveal(king.Id, null);

        room.ChooseKing(king.Id, ana.Id);

        Assert.Equal(ana.Id, room.KingPlayerId);
        Assert.Equal(0, room.GetPosition(ana.Id)); // new King leaves the stairs
        Assert.Equal(1, room.GetPosition(bia.Id));
        Assert.Equal(GamePhase.KingPicking, room.Phase);
    }

    [Fact]
    public void Used_bank_questions_are_tracked_after_reveal()
    {
        var (room, king, ana, _) = StartedRoom();
        Ask(room, king, sourceId: "cor-favorita");
        Assert.Empty(room.GetStateFor(ana.Id).UsedQuestionIds);
        room.Reveal(king.Id);
        Assert.Equal(["cor-favorita"], room.GetStateFor(ana.Id).UsedQuestionIds);
    }
}
