using FamiliaQuiz.Shared;

namespace FamiliaQuiz.Server.Game;

/// <summary>
/// A themed special-round question with fixed picture options,
/// e.g. "Which weapon would I pick for a zombie apocalypse?".
/// </summary>
public sealed class SpecialTheme
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public List<RoundOptionDto> Options { get; set; } = [];
}
