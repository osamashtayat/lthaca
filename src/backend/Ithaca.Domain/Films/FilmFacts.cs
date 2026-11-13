using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Films;

/// <summary>
/// AI-safe facts about a <see cref="Film"/>, from Wikidata only (ADR-0017), and the text its embedding is built from.
/// One row per film that Wikidata links to our TMDB id; films Wikidata doesn't know have none. Wikidata is CC0, so the
/// TMDB 180-day rule doesn't apply; the row goes when its film is purged.
/// </summary>
public sealed class FilmFacts
{
    public const int WikidataIdMaxLength = 32;
    public const int MaxGenres = 10;
    public const int MaxMainSubjects = 15;
    public const int MaxDirectors = 5;
    public const int MaxCast = 10;
    public const int MaxCountries = 5;
    public const int MaxLanguages = 5;
    public const int MaxAwards = 15;
    public const int MaxLabelLength = 200;

    private FilmFacts(Guid filmId)
    {
        FilmId = filmId;
    }

    /// <summary>For persistence materialization only.</summary>
    private FilmFacts()
    {
    }

    public Guid FilmId { get; private set; }

    /// <summary>The Wikidata item, e.g. <c>Q190050</c>.</summary>
    public string WikidataId { get; private set; } = "";

    public string[] Genres { get; private set; } = [];

    public string[] MainSubjects { get; private set; } = [];

    public string[] Directors { get; private set; } = [];

    /// <summary>Best-known first, at most <see cref="MaxCast"/>.</summary>
    public string[] Cast { get; private set; } = [];

    public string[] Countries { get; private set; } = [];

    public string[] Languages { get; private set; } = [];

    public int? ReleaseYear { get; private set; }

    public int? RuntimeMinutes { get; private set; }

    public string[] Awards { get; private set; } = [];

    /// <summary>What the film's vector represents (the sync embeds it). Built from the fields above only, never from TMDB.</summary>
    public string EmbeddingText { get; private set; } = "";

    /// <summary><see cref="HashOf"/> of <see cref="EmbeddingText"/>; compared with <see cref="Film.EmbeddingSourceHash"/>.</summary>
    public string EmbeddingTextHash { get; private set; } = "";

    public DateTimeOffset FetchedAt { get; private set; }

    public static FilmFacts Create(Guid filmId, WikidataFacts facts, DateTimeOffset now)
    {
        if (filmId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "filmId is required.", "filmId");
        }

        var created = new FilmFacts(filmId);
        created.Update(facts, now);
        return created;
    }

    /// <summary>
    /// Replaces every fact with Wikidata's current answer. When the text changes, so does <see cref="EmbeddingTextHash"/>,
    /// which tells the sync to re-embed the film.
    /// </summary>
    public void Update(WikidataFacts facts, DateTimeOffset now)
    {
        var clean = Normalize(facts);
        var text = BuildEmbeddingText(clean);
        WikidataId = clean.WikidataId;
        Genres = [.. clean.Genres];
        MainSubjects = [.. clean.MainSubjects];
        Directors = [.. clean.Directors];
        Cast = [.. clean.Cast];
        Countries = [.. clean.Countries];
        Languages = [.. clean.Languages];
        ReleaseYear = clean.ReleaseYear;
        RuntimeMinutes = clean.RuntimeMinutes;
        Awards = [.. clean.Awards];
        EmbeddingText = text;
        EmbeddingTextHash = HashOf(text);
        FetchedAt = now;
    }

    /// <summary>
    /// The embedding text: one labelled line per non-empty fact, in a fixed order. Deterministic: the same facts, in any
    /// order (except cast, which keeps its ranking), give the same text. Changing it means re-embedding the catalog.
    /// Its only input is <see cref="WikidataFacts"/>, so TMDB-derived text can't reach it.
    /// </summary>
    public static string BuildEmbeddingText(WikidataFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var clean = Normalize(facts);
        var text = new StringBuilder();
        AppendLine(text, "Genres", clean.Genres);
        AppendLine(text, "Subjects", clean.MainSubjects);
        AppendLine(text, "Directed by", clean.Directors);
        AppendLine(text, "Starring", clean.Cast);
        AppendLine(text, "Countries", clean.Countries);
        AppendLine(text, "Languages", clean.Languages);
        if (clean.ReleaseYear is { } year)
        {
            text.Append("Year: ").Append(year.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        if (clean.RuntimeMinutes is { } minutes)
        {
            text.Append("Runtime: ").Append(minutes.ToString(CultureInfo.InvariantCulture)).Append(" minutes\n");
        }

        AppendLine(text, "Awards", clean.Awards);
        return text.ToString().TrimEnd('\n');
    }

    /// <summary>Lowercase hex SHA-256 of the UTF-8 text: a stable fingerprint of what was embedded.</summary>
    public static string HashOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static void AppendLine(StringBuilder text, string label, IReadOnlyList<string> values)
    {
        if (values.Count > 0)
        {
            text.Append(label).Append(": ").AppendJoin(", ", values).Append('\n');
        }
    }

    /// <summary>Trimmed, de-duplicated, capped; every list sorted except cast. Bad ids and impossible numbers are rejected or dropped.</summary>
    private static WikidataFacts Normalize(WikidataFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var id = facts.WikidataId?.Trim() ?? "";
        if (!IsWikidataId(id))
        {
            throw new DomainException(ErrorCodes.Validation, "wikidataId must look like Q123.", "wikidataId");
        }

        return new WikidataFacts(
            id,
            Sorted(facts.Genres, MaxGenres),
            Sorted(facts.MainSubjects, MaxMainSubjects),
            Sorted(facts.Directors, MaxDirectors),
            Clean(facts.Cast).Take(MaxCast).ToList(),
            Sorted(facts.Countries, MaxCountries),
            Sorted(facts.Languages, MaxLanguages),
            facts.ReleaseYear is >= 1850 and <= 2200 ? facts.ReleaseYear : null,
            facts.RuntimeMinutes is > 0 and <= 1000 ? facts.RuntimeMinutes : null,
            Sorted(facts.Awards, MaxAwards));
    }

    private static List<string> Sorted(IReadOnlyList<string>? values, int max) =>
        [.. Clean(values).Order(StringComparer.OrdinalIgnoreCase).ThenBy(v => v, StringComparer.Ordinal).Take(max)];

    private static IEnumerable<string> Clean(IReadOnlyList<string>? values) =>
        (values ?? [])
            .Select(v => v?.Trim() ?? "")
            .Where(v => v.Length is > 0 and <= MaxLabelLength)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static bool IsWikidataId(string id) =>
        id.Length is > 1 and <= WikidataIdMaxLength && id[0] == 'Q' && id.Skip(1).All(char.IsAsciiDigit);
}
