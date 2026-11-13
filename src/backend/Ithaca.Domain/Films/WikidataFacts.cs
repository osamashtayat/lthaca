namespace Ithaca.Domain.Films;

/// <summary>
/// What Wikidata (CC0) says about one film: the only film data the LLM and the embedding model may see (ADR-0017).
/// Every list holds English labels. <see cref="Cast"/> is best-known first; the other lists have no meaningful order.
/// Nothing here may come from TMDB.
/// </summary>
public sealed record WikidataFacts(
    string WikidataId,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> MainSubjects,
    IReadOnlyList<string> Directors,
    IReadOnlyList<string> Cast,
    IReadOnlyList<string> Countries,
    IReadOnlyList<string> Languages,
    int? ReleaseYear,
    int? RuntimeMinutes,
    IReadOnlyList<string> Awards);
