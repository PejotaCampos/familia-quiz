using FamiliaQuiz.Server.Game;
using FamiliaQuiz.Shared;

namespace FamiliaQuiz.Server.Tests;

public class SpecialRoundTests
{
    private static readonly SpecialTheme Zombie = new()
    {
        Id = "arma-zumbi",
        Text = "Qual arma eu escolheria para um apocalipse zumbi?",
        Options =
        [
            new() { Text = "Katana", Emoji = "🗡️" },
            new() { Text = "Taco", Emoji = "⚾", Image = "arma-zumbi/taco.webp" },
            new() { Text = "Arco", Emoji = "🏹" },
            new() { Text = "Machado", Emoji = "🪓" },
        ],
    };

    /// <summary>5 steps (middle = 3). Pedro is King; Ana and Bia guess.</summary>
    private static (GameRoom room, Player king, Player ana, Player bia) StartedRoom(params SpecialTheme[] themes)
    {
        var room = new GameRoom("ABCD", 5, themes.Length == 0 ? [Zombie] : themes);
        var king = room.Join("Pedro");
        var ana = room.Join("Ana");
        var bia = room.Join("Bia");
        foreach (var p in new[] { king, ana, bia }) room.SetConnected(p.Id, true);
        room.StartGame(king.Id);
        room.ChooseKing(king.Id, king.Id);
        return (room, king, ana, bia);
    }

    /// <summary>Plays a normal question where only Ana is right, then reveals.</summary>
    private static void AnaScores(GameRoom room, Player king, Player ana, Player bia)
    {
        room.SubmitQuestion(king.Id, "Q?", ["a", "b"], 0, null);
        room.SubmitGuess(ana.Id, 0);
        room.SubmitGuess(bia.Id, 1);
    }

    private static void ClimbToMiddle(GameRoom room, Player king, Player ana, Player bia)
    {
        for (var i = 0; i < room.MiddleStep; i++)
        {
            AnaScores(room, king, ana, bia);
            if (i < room.MiddleStep - 1) room.ContinueAfterReveal(king.Id, null);
        }
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(6, 3)]
    [InlineData(10, 5)]
    public void Middle_step_is_half_way_rounded_up(int steps, int middle) =>
        Assert.Equal(middle, new GameRoom("ABCD", steps).MiddleStep);

    [Fact]
    public void Reaching_the_middle_makes_the_next_round_special()
    {
        var (room, king, ana, bia) = StartedRoom();
        Assert.Equal(3, room.GetStateFor(ana.Id).SpecialStep);

        AnaScores(room, king, ana, bia); // Ana at step 1: nothing special yet
        Assert.False(room.GetStateFor(ana.Id).NextRoundIsSpecial);
        room.ContinueAfterReveal(king.Id, null);
        Assert.Equal(GamePhase.KingPicking, room.Phase);

        AnaScores(room, king, ana, bia);
        room.ContinueAfterReveal(king.Id, null);
        AnaScores(room, king, ana, bia); // Ana reaches step 3 = middle

        Assert.True(room.GetStateFor(ana.Id).NextRoundIsSpecial);
        room.ContinueAfterReveal(king.Id, null);
        Assert.Equal(GamePhase.SpecialPicking, room.Phase);
    }

    [Fact]
    public void Special_round_flow_moves_correct_guessers_up()
    {
        var (room, king, ana, bia) = StartedRoom();
        ClimbToMiddle(room, king, ana, bia);

        Assert.True(room.GetStateFor(bia.Id).NextRoundIsSpecial);
        room.ContinueAfterReveal(king.Id, null);

        // Everyone sees the themed options while the King chooses; there is no answer yet.
        Assert.Equal(GamePhase.SpecialPicking, room.Phase);
        var biaView = room.GetStateFor(bia.Id);
        Assert.Equal(RoundKind.Special, biaView.Round!.Kind);
        Assert.Equal("arma-zumbi", biaView.Round.ThemeId);
        Assert.Equal("arma-zumbi/taco.webp", biaView.Round.Options[1].Image);
        Assert.Null(biaView.CorrectOptionIndex);
        Assert.Null(biaView.SpecialStep); // used up for this reign

        Assert.Equal(ErrorCode.NotKing, Assert.Throws<GameException>(() => room.SubmitSpecialAnswer(bia.Id, 0)).Code);
        Assert.Equal(ErrorCode.WrongPhase, Assert.Throws<GameException>(() => room.SubmitGuess(bia.Id, 0)).Code);

        room.SubmitSpecialAnswer(king.Id, 2);
        Assert.Equal(GamePhase.Guessing, room.Phase);
        Assert.Null(room.GetStateFor(bia.Id).CorrectOptionIndex);
        Assert.Equal(2, room.GetStateFor(king.Id).CorrectOptionIndex);

        room.SubmitGuess(ana.Id, 0);
        room.SubmitGuess(bia.Id, 2);

        Assert.Equal(GamePhase.Reveal, room.Phase);
        Assert.Equal(3, room.GetPosition(ana.Id));
        Assert.Equal(1, room.GetPosition(bia.Id));
    }

    [Fact]
    public void Only_one_special_round_per_reign()
    {
        var (room, king, ana, bia) = StartedRoom();
        ClimbToMiddle(room, king, ana, bia);
        room.ContinueAfterReveal(king.Id, null);
        room.SubmitSpecialAnswer(king.Id, 0);
        room.SubmitGuess(ana.Id, 0);
        room.SubmitGuess(bia.Id, 0);

        Assert.False(room.GetStateFor(ana.Id).NextRoundIsSpecial);
        room.ContinueAfterReveal(king.Id, null);
        Assert.Equal(GamePhase.KingPicking, room.Phase);
    }

    [Fact]
    public void Skipping_the_special_round_uses_it_up()
    {
        var (room, king, ana, bia) = StartedRoom();
        ClimbToMiddle(room, king, ana, bia);
        room.ContinueAfterReveal(king.Id, null);

        room.SkipQuestion(king.Id);

        Assert.Equal(GamePhase.KingPicking, room.Phase);
        Assert.Null(room.GetStateFor(ana.Id).Round);
        AnaScores(room, king, ana, bia);
        Assert.False(room.GetStateFor(ana.Id).NextRoundIsSpecial);
    }

    [Fact]
    public void A_new_reign_gets_a_new_special_round()
    {
        var (room, king, ana, bia) = StartedRoom();
        ClimbToMiddle(room, king, ana, bia);
        room.ContinueAfterReveal(king.Id, null);
        room.SkipQuestion(king.Id);

        room.ChooseKing(king.Id, bia.Id);

        Assert.Equal(3, room.GetStateFor(ana.Id).SpecialStep);
    }

    [Fact]
    public void Crowning_wins_over_a_special_round()
    {
        // Ana is near the top when the King is replaced; her next correct answer both
        // crosses the middle (new reign) and reaches the top.
        var (room, king, ana, bia) = StartedRoom();
        for (var i = 0; i < 4; i++)
        {
            AnaScores(room, king, ana, bia);
            if (i == 2) { room.ContinueAfterReveal(king.Id, null); room.SkipQuestion(king.Id); continue; }
            if (i < 3) room.ContinueAfterReveal(king.Id, null);
        }
        room.ContinueAfterReveal(king.Id, null);
        room.ChooseKing(king.Id, king.Id); // new reign, Ana still at step 4

        AnaScores(room, king, ana, bia);

        var state = room.GetStateFor(ana.Id);
        Assert.Equal([ana.Id], state.TopPlayerIds);
        Assert.False(state.NextRoundIsSpecial);
    }

    [Fact]
    public void Without_themes_there_are_no_special_rounds()
    {
        var room = new GameRoom("ABCD", 5);
        var king = room.Join("Pedro");
        var ana = room.Join("Ana");
        var bia = room.Join("Bia");
        foreach (var p in new[] { king, ana, bia }) room.SetConnected(p.Id, true);
        room.StartGame(king.Id);
        room.ChooseKing(king.Id, king.Id);

        Assert.Null(room.GetStateFor(ana.Id).SpecialStep);
        ClimbToMiddle(room, king, ana, bia);
        Assert.False(room.GetStateFor(ana.Id).NextRoundIsSpecial);
    }

    [Fact]
    public void Themes_are_not_repeated_until_all_were_played()
    {
        var path = new SpecialTheme { Id = "caminho", Text = "Caminho?", Options = [new() { Text = "A" }, new() { Text = "B" }] };
        var (room, king, ana, bia) = StartedRoom(Zombie, path);

        ClimbToMiddle(room, king, ana, bia);
        room.ContinueAfterReveal(king.Id, null);
        var first = room.GetStateFor(ana.Id).Round!.ThemeId;
        room.SkipQuestion(king.Id);

        room.ChooseKing(king.Id, king.Id); // new reign; Ana is still past the middle
        AnaScores(room, king, ana, bia);
        room.ContinueAfterReveal(king.Id, null);
        var second = room.GetStateFor(ana.Id).Round!.ThemeId;

        Assert.NotEqual(first, second);
    }
}
