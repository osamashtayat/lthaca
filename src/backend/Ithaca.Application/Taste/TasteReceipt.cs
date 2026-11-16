namespace Ithaca.Application.Taste;

/// <summary>
/// The taste receipt of <c>GET /api/taste</c> (api-contract.md §12). Pure, so it is unit-tested without a database. Genres are
/// catalog display data, never sent to AI (ADR-0017).
/// </summary>
public static class TasteReceipt
{
    /// <summary>Lines in <c>topGenres</c> and in <c>notForYou</c>.</summary>
    public const int MaxGenres = 3;

    /// <summary>A genre is "not for you" only when at least this many turned-down films have it (one left swipe is no verdict).</summary>
    public const int MinTurnedDown = 2;

    /// <summary>
    /// Each genre's share of all genre mentions among the positive-signal films (a film with two genres counts once for each), rounded
    /// to 1–100; up to <see cref="MaxGenres"/>, biggest first, then by name.
    /// </summary>
    public static IReadOnlyList<TasteGenreDto> TopGenres(IReadOnlyList<IReadOnlyList<string>> liked)
    {
        ArgumentNullException.ThrowIfNull(liked);
        var counts = Count(liked);
        var total = counts.Values.Sum();
        return counts
            .OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal)
            .Take(MaxGenres)
            .Select(c => new TasteGenreDto(c.Key, Math.Clamp((int)Math.Round(100.0 * c.Value / total, MidpointRounding.AwayFromZero), 1, 100)))
            .ToList();
    }

    /// <summary>
    /// Genres the user turns down: in at least <see cref="MinTurnedDown"/> turned-down films, and a larger share of the
    /// turned-down films than of the positive-signal ones; never one of <paramref name="topGenres"/>. Up to <see cref="MaxGenres"/>,
    /// the biggest difference first, then by name.
    /// </summary>
    public static IReadOnlyList<string> NotForYou(
        IReadOnlyList<IReadOnlyList<string>> liked, IReadOnlyList<IReadOnlyList<string>> turnedDown, IReadOnlyList<TasteGenreDto> topGenres)
    {
        ArgumentNullException.ThrowIfNull(liked);
        ArgumentNullException.ThrowIfNull(turnedDown);
        ArgumentNullException.ThrowIfNull(topGenres);
        var likedCounts = Count(liked);
        var top = topGenres.Select(g => g.Genre).ToHashSet(StringComparer.Ordinal);
        return Count(turnedDown)
            .Where(c => c.Value >= MinTurnedDown && !top.Contains(c.Key))
            .Select(c => (Genre: c.Key, Score: Share(c.Value, turnedDown.Count) - Share(likedCounts.GetValueOrDefault(c.Key), liked.Count)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.Genre, StringComparer.Ordinal)
            .Take(MaxGenres)
            .Select(x => x.Genre)
            .ToList();
    }

    private static Dictionary<string, int> Count(IReadOnlyList<IReadOnlyList<string>> films)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var genre in films.SelectMany(f => f.Distinct(StringComparer.Ordinal)))
        {
            counts[genre] = counts.GetValueOrDefault(genre) + 1;
        }

        return counts;
    }

    private static double Share(int count, int films) => films == 0 ? 0 : (double)count / films;
}
