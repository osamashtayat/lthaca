using Ithaca.Domain.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Moods;

/// <summary>
/// A mood request (glossary: <c>MoodQuery</c>): the tapped moods, runtime and language as given, the free text, and the
/// filters we derived from them (chips by <see cref="MoodChips"/>, text by <c>mood-parse</c>, validated against our genres).
/// <see cref="TextIgnored"/>: the text wasn't a mood, so only the chips were used (ADR-0022). Owned by a solo deck now; the
/// room mood step (BE-11) stores one per member.
/// </summary>
public sealed class MoodQuery
{
    public const int MaxMoods = 2;
    public const int TextMaxLength = 280;
    public const int LanguageLength = 2;
    public const int MaxGenres = 20;
    public const int MaxKeywords = 10;
    public const int KeywordMaxLength = 50;

    /// <summary>Release years before this aren't a real filter (the first films).</summary>
    public const int EarliestYear = 1870;

    private MoodQuery(Guid userId, DateTimeOffset now)
    {
        Id = SequentialGuid.NewV7(now);
        UserId = userId;
        CreatedAt = now;
    }

    /// <summary>For persistence materialization only.</summary>
    private MoodQuery()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Mood[] Moods { get; private set; } = [];

    public RuntimeLimit MaxRuntime { get; private set; }

    /// <summary>ISO 639-1 (lowercase), or null for any language.</summary>
    public string? Language { get; private set; }

    /// <summary>The person's own words, kept for their own deck only; never logged, never put in a message (ADR-0022).</summary>
    public string? Text { get; private set; }

    public string[] IncludeGenres { get; private set; } = [];

    public string[] ExcludeGenres { get; private set; } = [];

    public int? MaxRuntimeMinutes { get; private set; }

    public int? MinReleaseYear { get; private set; }

    public int? MaxReleaseYear { get; private set; }

    public string[] Keywords { get; private set; } = [];

    public bool TextIgnored { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The input as given, normalised: moods distinct, language lowercase, blank text null.</summary>
    public static MoodQuery Create(Guid userId, IEnumerable<Mood> moods, RuntimeLimit maxRuntime, string? language, string? text, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(moods);
        if (userId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "userId is required.");
        }

        var distinct = moods.Distinct().ToArray();
        if (distinct.Length > MaxMoods)
        {
            throw new DomainException(ErrorCodes.Validation, $"Pick at most {MaxMoods} moods.", "moods");
        }

        var trimmedText = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        if (trimmedText is { Length: > TextMaxLength })
        {
            throw new DomainException(ErrorCodes.Validation, $"text must be at most {TextMaxLength} characters.", "text");
        }

        var lang = string.IsNullOrWhiteSpace(language) ? null : language.Trim().ToLowerInvariant();
        if (lang is not null && (lang.Length != LanguageLength || !lang.All(char.IsAsciiLetterLower)))
        {
            throw new DomainException(ErrorCodes.Validation, "language must be a two-letter ISO 639-1 code.", "language");
        }

        return new MoodQuery(userId, now) { Moods = distinct, MaxRuntime = maxRuntime, Language = lang, Text = trimmedText };
    }

    /// <summary>
    /// Stores the derived filters. The caller has already validated genres against the catalog; lists are de-duplicated and
    /// capped here, an included genre is never also excluded, a year outside <see cref="EarliestYear"/>…next year is dropped,
    /// and so is an inverted year range.
    /// </summary>
    public void SetFilters(
        IEnumerable<string> includeGenres, IEnumerable<string> excludeGenres, int? maxRuntimeMinutes, int? minReleaseYear, int? maxReleaseYear,
        IEnumerable<string> keywords, bool textIgnored)
    {
        ArgumentNullException.ThrowIfNull(includeGenres);
        ArgumentNullException.ThrowIfNull(excludeGenres);
        ArgumentNullException.ThrowIfNull(keywords);
        IncludeGenres = includeGenres.Distinct(StringComparer.Ordinal).Take(MaxGenres).ToArray();
        ExcludeGenres = excludeGenres.Distinct(StringComparer.Ordinal).Except(IncludeGenres, StringComparer.Ordinal).Take(MaxGenres).ToArray();
        MaxRuntimeMinutes = maxRuntimeMinutes is > 0 ? maxRuntimeMinutes : null;
        var from = PlausibleYear(minReleaseYear);
        var to = PlausibleYear(maxReleaseYear);
        (MinReleaseYear, MaxReleaseYear) = from is { } min && to is { } max && min > max ? (null, null) : (from, to);
        Keywords = keywords
            .Select(k => k.Trim())
            .Where(k => k.Length is > 0 and <= KeywordMaxLength)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxKeywords)
            .ToArray();
        TextIgnored = textIgnored;
    }

    private int? PlausibleYear(int? year) => year is { } y && y >= EarliestYear && y <= CreatedAt.Year + 1 ? y : null;
}
