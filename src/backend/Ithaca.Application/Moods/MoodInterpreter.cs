using Ithaca.Application.Ai;
using Ithaca.Application.Common;
using Ithaca.Application.Films;
using Ithaca.Domain.Moods;

namespace Ithaca.Application.Moods;

/// <summary>
/// Turns what someone picked on the mood screen into a <see cref="MoodQuery"/> (not saved here). A collaborator of the deck
/// service now and of the room mood step later (BE-11), not a use-case service. The rules (ADR-0022, api-contract.md §5):
/// <list type="bullet">
/// <item>Chips → genres by <see cref="MoodChips"/>; runtime and language are plain filters. No LLM.</item>
/// <item>Empty text → no LLM call.</item>
/// <item>Text → <c>mood-parse</c> (the text is only ever the user message). Its genres are kept only when they exist in our
/// catalog; filters count only when it labels the text a mood.</item>
/// <item>Not a mood: with chips → chips only and <see cref="MoodQuery.TextIgnored"/>; without chips → <c>mood.unclear</c>.</item>
/// <item>A chip's genre is never excluded by the text: chips are the explicit choice.</item>
/// <item>The text is never logged or put in a message.</item>
/// </list>
/// Model failures pass through as <c>ai.refused</c> / <c>ai.invalid_output</c> / <c>ai.unavailable</c>.
/// </summary>
public sealed class MoodInterpreter(IAppDbContext db, ILlmClient llm, IPromptLibrary prompts, TimeProvider time)
{
    /// <summary>Generous for the small JSON reply.</summary>
    private const int MaxOutputTokens = 512;

    public async Task<Result<MoodQuery>> InterpretAsync(Guid userId, MoodInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var now = time.GetUtcNow();
        var query = MoodQuery.Create(userId, input.Moods, input.MaxRuntime, input.Language, input.Text, now);
        var chips = MoodChips.For(query.Moods);
        var runtime = RuntimeMinutes(query.MaxRuntime);

        if (query.Text is null)
        {
            query.SetFilters(chips.IncludeGenres, chips.ExcludeGenres, runtime, null, null, [], textIgnored: false);
            return query;
        }

        var genres = await CatalogGenres.ListAsync(db, ct);
        var prompt = await prompts.RenderAsync(
            Prompts.MoodParse,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["genres"] = string.Join(", ", genres),
                ["year"] = now.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            ct);
        var reply = await llm.CompleteAsync(new LlmRequest(prompt, query.Text, MaxOutputTokens), ct);
        if (!reply.IsSuccess)
        {
            return reply.Error;
        }

        var parsed = StructuredOutput.Parse<MoodParseOutput>(reply.Value);
        if (!parsed.IsSuccess)
        {
            return parsed.Error;
        }

        var text = parsed.Value;
        if (text.Intent != MoodIntent.Mood)
        {
            if (query.Moods.Length == 0)
            {
                return MoodErrors.Unclear;
            }

            query.SetFilters(chips.IncludeGenres, chips.ExcludeGenres, runtime, null, null, [], textIgnored: true);
            return query;
        }

        var known = genres.ToDictionary(g => g, g => g, StringComparer.OrdinalIgnoreCase);
        var include = chips.IncludeGenres.Concat(Known(text.IncludeGenres, known));
        var exclude = chips.ExcludeGenres.Concat(Known(text.ExcludeGenres, known)).Except(chips.IncludeGenres, StringComparer.Ordinal);
        query.SetFilters(
            include,
            exclude,
            Stricter(runtime, text.MaxRuntimeMinutes),
            text.MinReleaseYear,
            text.MaxReleaseYear,
            text.Keywords,
            textIgnored: false);
        return query;
    }

    /// <summary><see cref="RuntimeLimit"/> as minutes: under 90 → 89, up to 2h → 120, any → none.</summary>
    public static int? RuntimeMinutes(RuntimeLimit limit) => limit switch
    {
        RuntimeLimit.Under90 => 89,
        RuntimeLimit.UpTo2h => 120,
        _ => null,
    };

    /// <summary>The model's genres that exist in our catalog, in our spelling; anything else is dropped.</summary>
    private static IEnumerable<string> Known(IEnumerable<string> genres, Dictionary<string, string> known) =>
        genres.Select(g => known.GetValueOrDefault(g.Trim())).OfType<string>();

    private static int? Stricter(int? a, int? b) => (a, b) switch
    {
        (null, _) => b,
        (_, null) => a,
        _ => Math.Min(a.Value, b.Value),
    };
}
