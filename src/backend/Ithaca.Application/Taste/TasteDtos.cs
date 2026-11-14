namespace Ithaca.Application.Taste;

/// <summary>
/// <c>GET /api/taste</c> (api-contract.md §12): whether By taste is ready for the caller, and the "taste receipt". The
/// server decides readiness; the screen only shows it (FE-13: not ready → the import screen).
/// </summary>
/// <param name="Ready">True when <paramref name="SignalCount"/> ≥ <paramref name="SignalsNeeded"/>.</param>
/// <param name="SignalCount">Films in My Cinema with a positive signal: liked, rated ≥ 3.5, or "loved it".</param>
/// <param name="SignalsNeeded">The threshold (10, a config value BE-14 can tune; A16).</param>
/// <param name="LoggedCount">Films in My Cinema marked seen or rated ("From 214 films you've logged").</param>
/// <param name="TopGenres">Up to 3, biggest share first; empty when not ready.</param>
/// <param name="NotForYou">Up to 3 genres the caller turns down ("NOT FOR YOU: HORROR, HEISTS"); empty when not ready.</param>
public sealed record TasteDto(
    bool Ready,
    int SignalCount,
    int SignalsNeeded,
    int LoggedCount,
    IReadOnlyList<TasteGenreDto> TopGenres,
    IReadOnlyList<string> NotForYou);

/// <summary>One receipt line: a catalog genre (as in <c>FilmDto.genres</c>) and its share of the positive signals, 1–100.</summary>
public sealed record TasteGenreDto(string Genre, int Percent);
