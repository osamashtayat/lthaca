namespace Ithaca.Application.Ai;

/// <summary>
/// Typed output contract of <c>prompts/mood-parse.v1.md</c>. Changing its shape means a new prompt version.
/// The mood feature (not built yet) maps this onto its own <c>MoodQuery</c> filters.
/// </summary>
public sealed record MoodParseOutput(
    IReadOnlyList<string> IncludeGenres,
    IReadOnlyList<string> ExcludeGenres,
    int? MaxRuntimeMinutes,
    int? MinReleaseYear,
    int? MaxReleaseYear,
    IReadOnlyList<string> Keywords);

/// <summary>Typed output of <c>prompts/rank-candidates.v1.md</c>: TMDB ids taken ONLY from the candidates we sent.</summary>
public sealed record RankCandidatesOutput(IReadOnlyList<RankedCandidate> Ranked);

public sealed record RankedCandidate(int TmdbId, string Reason);
