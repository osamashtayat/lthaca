using Ithaca.Application.Ai;
using Ithaca.Application.Common;
using Ithaca.Application.Films;
using Ithaca.Domain.Moods;
using Microsoft.Extensions.Logging;

namespace Ithaca.Application.Moods;

/// <summary>
/// Turns what someone picked on the mood screen into a <see cref="MoodQuery"/> (not saved here). A collaborator of the deck
/// service and of the room mood step (BE-11; a guest has no user id), not a use-case service. The rules (ADR-0022, api-contract.md §5):
/// <list type="bullet">
/// <item>Chips → genres by <see cref="MoodChips"/>; runtime and language are plain filters. No LLM.</item>
/// <item>Empty text → no LLM call.</item>
/// <item>Text → <c>mood-parse</c> (the text is only ever the user message). Its genres are kept only when they exist in our
/// catalog; filters count only when it labels the text a mood.</item>
/// <item>Not a mood: with chips → chips only and <see cref="MoodQuery.TextIgnored"/>; without chips → <c>mood.unclear</c>.</item>
/// <item>A chip's genre is never excluded by the text: chips are the explicit choice.</item>
/// <item>The text is never logged or put in a message.</item>
/// <item>The text can't be read (owner, 2026-11-16; BE-19): provider down or slower than <see cref="Timeout"/>, refusal,
/// invalid reply, or the daily AI budget spent → with chips: chips only, <see cref="MoodQuery.TextIgnored"/> and
/// <see cref="MoodQuery.TextUnavailable"/>; without chips → <c>mood.text_unavailable</c>. Never an <c>ai.*</c> error.</item>
/// </list>
/// </summary>
public sealed partial class MoodInterpreter(IAppDbContext db, AiBudget llm, IPromptLibrary prompts, TimeProvider time, ILogger<MoodInterpreter> logger)
{
    /// <summary>Generous for the small JSON reply.</summary>
    private const int MaxOutputTokens = 512;

    /// <summary>Someone is waiting on the mood screen; after this, the text is treated as unreadable.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public async Task<Result<MoodQuery>> InterpretAsync(Guid? userId, MoodInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var now = time.GetUtcNow();
        var query = MoodQuery.Create(userId, input.Moods, input.MaxRuntime, input.Language, input.Text, now);
        var chips = MoodChips.For(query.Moods);
        var runtime = query.MaxRuntime.MaxMinutes();

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
        var parsed = await ParseAsync(prompt, query.Text, ct);
        if (!parsed.IsSuccess)
        {
            LogTextUnavailable(logger, prompt.Id, parsed.Error.Code);
            if (query.Moods.Length == 0)
            {
                return MoodErrors.TextUnavailable;
            }

            query.SetFilters(chips.IncludeGenres, chips.ExcludeGenres, runtime, null, null, [], textIgnored: true, textUnavailable: true);
            return query;
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
        var exclude = chips.ExcludeGenres.Concat(Known(text.ExcludeGenres, known)); // SetFilters drops any included genre
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

    /// <summary>The model's reading of the text, or why there is none (an AI error or a timeout as <c>ai.unavailable</c>).</summary>
    private async Task<Result<MoodParseOutput>> ParseAsync(RenderedPrompt prompt, string text, CancellationToken ct)
    {
        using var timeout = new CancellationTokenSource(Timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        Result<LlmResponse> reply;
        try
        {
            reply = await llm.CompleteAsync(new LlmRequest(prompt, text, MaxOutputTokens), linked.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return AiErrors.Unavailable;
        }

        return reply.IsSuccess ? StructuredOutput.Parse<MoodParseOutput>(reply.Value) : reply.Error;
    }

    /// <summary>The model's genres that exist in our catalog, in our spelling; anything else is dropped.</summary>
    private static IEnumerable<string> Known(IEnumerable<string> genres, Dictionary<string, string> known) =>
        genres.Select(g => known.GetValueOrDefault(g.Trim())).OfType<string>();

    private static int? Stricter(int? a, int? b) => (a, b) switch
    {
        (null, _) => b,
        (_, null) => a,
        _ => Math.Min(a.Value, b.Value),
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Prompt {Prompt} failed with {Code}; mood text not used")]
    private static partial void LogTextUnavailable(ILogger logger, PromptId prompt, string code);
}
