using Ithaca.Domain.Moods;

namespace Ithaca.Application.Decks;

/// <summary>
/// The hard limits on which films a deck may deal: a mood's filters (any included genre, no excluded genre, runtime, years,
/// language) and hard nos. A solo deck by mood uses its <see cref="MoodQuery"/> (<see cref="FromMood"/>); a room combines its
/// members' moods and hard nos (<see cref="ForRoom"/>). Applied by <see cref="DeckDealer"/>.
/// </summary>
public sealed record DeckFilter(
    IReadOnlyList<string> IncludeGenres,
    IReadOnlyList<string> ExcludeGenres,
    int? MaxRuntimeMinutes,
    int? MinReleaseYear,
    int? MaxReleaseYear,
    string? Language,
    HardNoFilter HardNos)
{
    /// <summary>No limits: the sampler over the whole catalog.</summary>
    public static readonly DeckFilter None = new([], [], null, null, null, null, HardNoRules.For([]));

    /// <summary>True when only hard nos (or nothing) limit the deck.</summary>
    public bool IsOnlyHardNos =>
        IncludeGenres.Count == 0 && ExcludeGenres.Count == 0 && MaxRuntimeMinutes is null && MinReleaseYear is null
        && MaxReleaseYear is null && Language is null;

    public static DeckFilter FromMood(MoodQuery? mood) =>
        mood is null
            ? None
            : None with
            {
                IncludeGenres = mood.IncludeGenres,
                ExcludeGenres = mood.ExcludeGenres,
                MaxRuntimeMinutes = mood.MaxRuntimeMinutes,
                MinReleaseYear = mood.MinReleaseYear,
                MaxReleaseYear = mood.MaxReleaseYear,
                Language = mood.Language,
            };

    /// <summary>
    /// A room's deck (owner, BE-11: "union, strictest runtime"). Any genre a member asked for is welcome; a genre a member ruled
    /// out is dropped unless another member asked for it; the shortest runtime limit wins. Years and language apply only when
    /// every member with a mood set one (then the widest). Every member's hard nos apply. No moods: no mood limits.
    /// </summary>
    public static DeckFilter ForRoom(IReadOnlyList<MoodQuery> moods, IEnumerable<HardNo> hardNos)
    {
        ArgumentNullException.ThrowIfNull(moods);
        var include = moods.SelectMany(m => m.IncludeGenres).Distinct(StringComparer.Ordinal).ToArray();
        var exclude = moods.SelectMany(m => m.ExcludeGenres).Distinct(StringComparer.Ordinal).Except(include, StringComparer.Ordinal).ToArray();
        var runtimes = moods.Select(m => m.MaxRuntimeMinutes).OfType<int>().ToList();
        var languages = moods.Select(m => m.Language).Distinct(StringComparer.Ordinal).ToList();
        return new DeckFilter(
            include,
            exclude,
            runtimes.Count > 0 ? runtimes.Min() : null,
            moods.Count > 0 && moods.All(m => m.MinReleaseYear is not null) ? moods.Min(m => m.MinReleaseYear) : null,
            moods.Count > 0 && moods.All(m => m.MaxReleaseYear is not null) ? moods.Max(m => m.MaxReleaseYear) : null,
            languages.Count == 1 ? languages[0] : null,
            HardNoRules.For(hardNos));
    }

    /// <summary>The same deck without its mood limits: the sampler, hard nos still excluded.</summary>
    public DeckFilter OnlyHardNos() => None with { HardNos = HardNos };
}
