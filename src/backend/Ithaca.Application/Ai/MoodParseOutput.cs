using System.Text.Json.Serialization;

namespace Ithaca.Application.Ai;

/// <summary>
/// Typed output contract of <c>prompts/mood-parse.v2.md</c> (v1 had no <see cref="Intent"/>). Changing its shape means a new
/// prompt version. <c>MoodInterpreter</c> validates it (genres against our catalog) before anything uses it.
/// </summary>
public sealed record MoodParseOutput(
    MoodIntent Intent,
    IReadOnlyList<string> IncludeGenres,
    IReadOnlyList<string> ExcludeGenres,
    int? MaxRuntimeMinutes,
    int? MinReleaseYear,
    int? MaxReleaseYear,
    IReadOnlyList<string> Keywords);

/// <summary>What the model thinks the text is (ADR-0022). It only labels; the server decides what happens.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MoodIntent>))]
public enum MoodIntent
{
    [JsonStringEnumMemberName("mood")]
    Mood,

    [JsonStringEnumMemberName("unclear")]
    Unclear,

    [JsonStringEnumMemberName("offTopic")]
    OffTopic,
}

/// <summary>Typed output of <c>prompts/rank-candidates.v1.md</c>: TMDB ids taken ONLY from the candidates we sent.</summary>
public sealed record RankCandidatesOutput(IReadOnlyList<RankedCandidate> Ranked);

public sealed record RankedCandidate(int TmdbId, string Reason);
