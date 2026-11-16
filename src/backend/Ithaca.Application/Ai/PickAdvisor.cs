using System.Globalization;
using System.Text.Json;
using Ithaca.Application.Common;
using Ithaca.Domain.Decks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ithaca.Application.Ai;

/// <summary>
/// The AI's advice on picks (BE-13): <c>rank-candidates</c> orders a solo deck's yeses with a reason each, and
/// <c>explain-pick</c> gives a reason for each of a room's picks. A collaborator of <c>DeckService</c> and
/// <c>RoomPickExplainer</c>, not a use-case service. Rules (docs/ai.md → AI picks):
/// <list type="bullet">
/// <item>The model sees Wikidata facts (<see cref="PickFacts"/>) and our own data only; films without facts aren't sent.</item>
/// <item>Every returned <c>tmdbId</c> must be one we sent; others and repeats are dropped. Reasons are cleaned by
/// <see cref="PickReason.Clean"/>.</item>
/// <item>The advice is optional: any failure (provider, refusal, invalid reply, timeout) gives null and a warning with
/// the prompt and error code only, and the caller shows its picks without reasons. So does a spent daily budget
/// (<see cref="AiBudget"/>).</item>
/// </list>
/// </summary>
public sealed partial class PickAdvisor(IAppDbContext db, AiBudget llm, IPromptLibrary prompts, TimeProvider time, ILogger<PickAdvisor> logger)
{
    /// <summary>A pick page waits for this at most; afterwards it shows the picks without reasons.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Generous for up to 20 short reasons.</summary>
    private const int MaxOutputTokens = 2048;

    /// <summary>The Wikidata facts of those films that have them, keyed by TMDB id.</summary>
    public async Task<Dictionary<int, PickFacts>> FactsAsync(IEnumerable<int> tmdbIds, CancellationToken ct)
    {
        var ids = tmdbIds.Distinct().ToList();
        var rows = await db.Films.AsNoTracking()
            .Where(f => ids.Contains(f.TmdbId))
            .Join(db.FilmFacts.AsNoTracking(), f => f.Id, x => x.FilmId, (f, x) => new { f.TmdbId, Facts = x })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.TmdbId, r => PickFacts.From(r.TmdbId, r.Facts));
    }

    /// <summary>The candidates best first, at most <paramref name="limit"/>, each with its reason; null when the AI can't help.</summary>
    public async Task<IReadOnlyList<AdvisedPick>?> RankAsync(string audience, int limit, RankCandidatesInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Candidates.Count == 0)
        {
            return null;
        }

        var variables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["audience"] = audience,
            ["limit"] = limit.ToString(CultureInfo.InvariantCulture),
        };
        var output = await AskAsync<RankCandidatesOutput>(Prompts.RankCandidates, variables, input, ct);
        return output is null ? null : Validated(output.Ranked, input.Candidates.Select(c => c.TmdbId)).Take(limit).ToList();
    }

    /// <summary>A reason for each pick the model explained, by TMDB id; null when the AI can't help.</summary>
    public async Task<Dictionary<int, string?>?> ExplainAsync(string audience, ExplainPicksInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Picks.Count == 0)
        {
            return null;
        }

        var variables = new Dictionary<string, string>(StringComparer.Ordinal) { ["audience"] = audience };
        var output = await AskAsync<ExplainPicksOutput>(Prompts.ExplainPick, variables, input, ct);
        return output is null ? null : Validated(output.Reasons, input.Picks.Select(p => p.TmdbId)).ToDictionary(p => p.TmdbId, p => p.Reason);
    }

    /// <summary>The model's picks that are among <paramref name="sent"/>, first mention only, in its order.</summary>
    private static IEnumerable<AdvisedPick> Validated(IReadOnlyList<RankedCandidate>? picks, IEnumerable<int> sent)
    {
        var allowed = sent.ToHashSet();
        return (picks ?? [])
            .Where(p => p is not null && allowed.Remove(p.TmdbId))
            .Select(p => new AdvisedPick(p.TmdbId, PickReason.Clean(p.Reason)));
    }

    private async Task<T?> AskAsync<T>(PromptId id, Dictionary<string, string> variables, object input, CancellationToken ct)
        where T : class
    {
        var prompt = await prompts.RenderAsync(id, variables, ct);
        using var timeout = new CancellationTokenSource(Timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        Result<LlmResponse> reply;
        try
        {
            reply = await llm.CompleteAsync(new LlmRequest(prompt, JsonSerializer.Serialize(input, Json), MaxOutputTokens), linked.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            LogTimedOut(logger, id, Timeout.TotalSeconds);
            return null;
        }

        Result<T> parsed = reply.IsSuccess ? StructuredOutput.Parse<T>(reply.Value) : reply.Error;
        if (!parsed.IsSuccess)
        {
            LogFailed(logger, id, parsed.Error.Code);
            return null;
        }

        return parsed.Value;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Prompt {Prompt} timed out after {Seconds} s; showing picks without reasons")]
    private static partial void LogTimedOut(ILogger logger, PromptId prompt, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Prompt {Prompt} failed with {Code}; showing picks without reasons")]
    private static partial void LogFailed(ILogger logger, PromptId prompt, string code);
}

/// <summary>A pick the model returned, validated against what we sent; <see cref="Reason"/> is null when its reason was unusable.</summary>
public sealed record AdvisedPick(int TmdbId, string? Reason);
