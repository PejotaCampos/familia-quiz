using System.Text.Json;
using FamiliaQuiz.Shared;

namespace FamiliaQuiz.Server.Questions;

/// <summary>
/// Seed questions loaded from Data/questions.json (pt-BR). Read-only for v1;
/// later this can move to a hosted Postgres behind the same class.
/// </summary>
public sealed class QuestionBank
{
    private readonly IReadOnlyList<QuestionDto> _questions;

    public QuestionBank(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "questions.json");
        using var stream = File.OpenRead(path);
        _questions = JsonSerializer.Deserialize<List<QuestionDto>>(stream, JsonSerializerOptions.Web) ?? [];
    }

    public IReadOnlyList<QuestionDto> All => _questions;
}
