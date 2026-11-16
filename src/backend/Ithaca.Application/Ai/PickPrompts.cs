using Ithaca.Domain.Films;

namespace Ithaca.Application.Ai;

/// <summary>
/// What the LLM may know about a film (ADR-0017): Wikidata facts only. Built from <see cref="FilmFacts"/> alone, never from
/// <c>Film</c> (TMDB's title, overview, tagline and keywords), so TMDB text can't reach a prompt. Lists are shortened to
/// keep the input small.
/// </summary>
public sealed record PickFacts(
    int TmdbId,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Subjects,
    IReadOnlyList<string> Directors,
    IReadOnlyList<string> Cast,
    IReadOnlyList<string> Countries,
    IReadOnlyList<string> Languages,
    int? Year,
    int? RuntimeMinutes,
    IReadOnlyList<string> Awards)
{
    private const int MaxSubjects = 8;
    private const int MaxCast = 5;
    private const int MaxAwards = 5;

    public static PickFacts From(int tmdbId, FilmFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return new PickFacts(
            tmdbId,
            facts.Genres,
            facts.MainSubjects.Take(MaxSubjects).ToList(),
            facts.Directors,
            facts.Cast.Take(MaxCast).ToList(),
            facts.Countries,
            facts.Languages,
            facts.ReleaseYear,
            facts.RuntimeMinutes,
            facts.Awards.Take(MaxAwards).ToList());
    }
}

/// <summary>
/// The user message of <c>rank-candidates.v2</c> (a solo top 3): the user's own signals and the candidates' facts. The genre
/// names come from our catalog's list and the user's choices, never TMDB text.
/// </summary>
public sealed record RankCandidatesInput(SoloTaste Taste, IReadOnlyList<PickFacts> Candidates);

/// <param name="MoodGenres">Genres the deck's mood asked for (chips and text, after our checks).</param>
/// <param name="MoodKeywords">The mood's keywords (BE-09 stored them for this).</param>
/// <param name="MaxRuntimeMinutes">The mood's runtime limit, if any.</param>
/// <param name="LikedGenres">Wikidata genres of films the user loved or liked, most frequent first.</param>
/// <param name="DislikedGenres">Wikidata genres of films the user swiped away in this deck, most frequent first.</param>
public sealed record SoloTaste(
    IReadOnlyList<string> MoodGenres,
    IReadOnlyList<string> MoodKeywords,
    int? MaxRuntimeMinutes,
    IReadOnlyList<string> LikedGenres,
    IReadOnlyList<string> DislikedGenres);

/// <summary>
/// The user message of <c>explain-pick.v2</c> (a room's result): only counts, never who voted what, nor anyone's mood or
/// hard nos (A7), so a reason can't single anyone out.
/// </summary>
/// <param name="Result"><c>match</c> or <c>compromise</c>.</param>
public sealed record ExplainPicksInput(string Result, int GroupSize, IReadOnlyList<GroupPick> Picks);

public sealed record GroupPick(int TmdbId, int YesCount, int NoCount, int SeenCount, PickFacts Facts);

/// <summary>Typed output of <c>prompts/explain-pick.v2.md</c>: one reason per given pick, ids ONLY from the picks we sent.</summary>
public sealed record ExplainPicksOutput(IReadOnlyList<RankedCandidate> Reasons);
