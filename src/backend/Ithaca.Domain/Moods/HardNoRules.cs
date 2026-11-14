namespace Ithaca.Domain.Moods;

/// <summary>
/// What each hard no rules out (owner, BE-11: "wide nets"; glossary: Hard no). The server-side table, like
/// <see cref="MoodChips"/>: our catalog's TMDB genres, Wikidata genre labels (English, matched as lowercase substrings;
/// coverage is partial, so each rule also has a TMDB genre where one fits), and "not in English" for subtitles. A film with
/// no original language recorded passes the subtitles rule.
/// </summary>
public static class HardNoRules
{
    /// <summary>The language a film needs to pass "no subtitles" (ISO 639-1).</summary>
    public const string NoSubtitlesLanguage = "en";

    public static readonly IReadOnlyDictionary<HardNo, HardNoFilter> Table = new Dictionary<HardNo, HardNoFilter>
    {
        [HardNo.Horror] = new(["Horror"], [], NonEnglish: false),
        [HardNo.Gore] = new(["Horror"], ["splatter", "slasher", "body horror", "zombie", "gore"], NonEnglish: false),
        [HardNo.Musicals] = new(["Music"], ["musical"], NonEnglish: false),
        [HardNo.SadEndings] = new(["War"], ["tragedy"], NonEnglish: false),
        [HardNo.Subtitles] = new([], [], NonEnglish: true),
    };

    /// <summary>Everything the given hard nos rule out, combined (any one rule excludes a film).</summary>
    public static HardNoFilter For(IEnumerable<HardNo> hardNos)
    {
        ArgumentNullException.ThrowIfNull(hardNos);
        var rules = hardNos.Distinct().Select(h => Table[h]).ToList();
        return new HardNoFilter(
            rules.SelectMany(r => r.TmdbGenres).Distinct(StringComparer.Ordinal).ToArray(),
            rules.SelectMany(r => r.WikidataGenreTerms).Distinct(StringComparer.Ordinal).ToArray(),
            rules.Any(r => r.NonEnglish));
    }
}

/// <summary>
/// Films a hard no rules out: any of <see cref="TmdbGenres"/>, a Wikidata genre label containing any of
/// <see cref="WikidataGenreTerms"/> (lowercase), or, with <see cref="NonEnglish"/>, a known original language other than English.
/// </summary>
public sealed record HardNoFilter(IReadOnlyList<string> TmdbGenres, IReadOnlyList<string> WikidataGenreTerms, bool NonEnglish)
{
    public bool IsEmpty => TmdbGenres.Count == 0 && WikidataGenreTerms.Count == 0 && !NonEnglish;
}
