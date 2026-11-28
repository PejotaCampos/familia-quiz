using System.Text.Json;
using FamiliaQuiz.Server.Game;
using FamiliaQuiz.Shared;

namespace FamiliaQuiz.Server.Questions;

/// <summary>
/// Special-round themes loaded from Data/special-rounds.json.
/// Images live in the client (wwwroot/images/special/); this file only holds their relative paths.
/// </summary>
public sealed class SpecialRoundBank
{
    public SpecialRoundBank(IWebHostEnvironment env, ILogger<SpecialRoundBank> logger)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "special-rounds.json");
        using var stream = File.OpenRead(path);
        var themes = JsonSerializer.Deserialize<List<SpecialTheme>>(stream, JsonSerializerOptions.Web) ?? [];

        // Skip broken entries instead of failing a whole game night over one typo.
        Themes = themes.Where(t => IsValid(t, logger)).ToList();
    }

    public IReadOnlyList<SpecialTheme> Themes { get; }

    private static bool IsValid(SpecialTheme theme, ILogger logger)
    {
        var ok = !string.IsNullOrWhiteSpace(theme.Id)
                 && !string.IsNullOrWhiteSpace(theme.Text)
                 && theme.Options.Count is >= GameLimits.MinOptions and <= GameLimits.MaxSpecialOptions
                 && theme.Options.Select(o => o.Text.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == theme.Options.Count
                 && theme.Options.All(o => !string.IsNullOrWhiteSpace(o.Text));
        if (!ok) logger.LogWarning("Ignoring invalid special round theme '{Id}'", theme.Id);
        return ok;
    }
}
