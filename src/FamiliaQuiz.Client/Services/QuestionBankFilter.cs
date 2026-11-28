using System.Globalization;
using System.Text;
using FamiliaQuiz.Shared;

namespace FamiliaQuiz.Client.Services;

/// <summary>
/// The King's question-bank filter. Registered as a service so it survives between questions
/// (the editor component is recreated for every new question).
/// </summary>
public sealed class QuestionBankFilter
{
    /// <summary>Selected category, or null for all categories.</summary>
    public string? Category { get; set; }

    public string Search { get; set; } = "";

    public bool ShowUsed { get; set; }

    public bool Matches(QuestionDto q, ICollection<string> usedIds) =>
        MatchesIgnoringCategory(q, usedIds) && (Category is null || q.Category == Category);

    /// <summary>Search and "show used" only; used to show how many questions each category chip has.</summary>
    public bool MatchesIgnoringCategory(QuestionDto q, ICollection<string> usedIds) =>
        (ShowUsed || !usedIds.Contains(q.Id)) && MatchesSearch(q);

    private bool MatchesSearch(QuestionDto q)
    {
        var term = Normalize(Search);
        if (term.Length == 0) return true;
        return Normalize(q.Text).Contains(term)
               || Normalize(q.Category).Contains(term)
               || q.Options.Any(o => Normalize(o).Contains(term));
    }

    /// <summary>Lower-case and without accents, so "infancia" finds "Infância".</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }
}
