namespace Ithaca.Domain.Moods;

/// <summary>
/// What each mood chip means, as filters on our catalog's genres (TMDB genre names). The server-side table the contract asks
/// for (api-contract.md §5): no LLM involved. Two chips combine: their included genres add up, and a genre one chip includes
/// is never excluded by the other. <see cref="Mood.Surprise"/> sets no filter: the sampler's variety is the surprise.
/// </summary>
public static class MoodChips
{
    public static readonly IReadOnlyDictionary<Mood, ChipFilters> Table = new Dictionary<Mood, ChipFilters>
    {
        [Mood.Cozy] = new(["Comedy", "Family", "Animation", "Romance"], ["Horror", "War", "Thriller", "Crime"]),
        [Mood.Laugh] = new(["Comedy"], ["Horror", "War"]),
        [Mood.KeepGuessing] = new(["Mystery", "Thriller", "Crime"], []),
        [Mood.MakeMeFeel] = new(["Drama", "Romance"], []),
        [Mood.TakeMeAway] = new(["Adventure", "Fantasy", "Science Fiction", "Animation"], []),
        [Mood.Surprise] = new([], []),
    };

    /// <summary>The combined filters of the tapped chips.</summary>
    public static ChipFilters For(IEnumerable<Mood> moods)
    {
        ArgumentNullException.ThrowIfNull(moods);
        var chips = moods.Distinct().Select(m => Table[m]).ToList();
        var include = chips.SelectMany(c => c.IncludeGenres).Distinct(StringComparer.Ordinal).ToArray();
        var exclude = chips.SelectMany(c => c.ExcludeGenres).Distinct(StringComparer.Ordinal).Except(include, StringComparer.Ordinal).ToArray();
        return new ChipFilters(include, exclude);
    }
}

/// <summary>Genres a chip asks for (any of them) and genres it rules out.</summary>
public sealed record ChipFilters(IReadOnlyList<string> IncludeGenres, IReadOnlyList<string> ExcludeGenres);
