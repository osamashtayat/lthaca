using Ithaca.Domain.Common;
using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Moods;

/// <summary>
/// A mood request (glossary: <c>MoodQuery</c>): the tapped moods, runtime and language as given, the free text, and the
/// filters we derived from them (chips by <see cref="MoodChips"/>, text by <c>mood-parse</c>, validated against our genres).
/// <see cref="TextIgnored"/>: the text wasn't used, so only the chips were (ADR-0022); <see cref="TextUnavailable"/> says it
/// wasn't read at all because the AI failed or its daily budget was used up (BE-19). Owned by a solo deck, or by a
/// room member's mood step (<see cref="RoomMemberId"/>; a guest's has no user), which keeps no text (issue #51).
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

    private MoodQuery(Guid? userId, DateTimeOffset now)
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

    /// <summary>The person who asked; null for a guest's room mood (still null after the guest signs up: it goes with the member).</summary>
    public Guid? UserId { get; private set; }

    public Mood[] Moods { get; private set; } = [];

    public RuntimeLimit MaxRuntime { get; private set; }

    /// <summary>ISO 639-1 (lowercase), or null for any language.</summary>
    public string? Language { get; private set; }

    /// <summary>
    /// The person's own words, kept for their own solo deck only (shown back on it); never logged, never put in a message
    /// (ADR-0022). Always null for a room mood: once interpreted, nothing reads it again (owner, issue #51).
    /// </summary>
    public string? Text { get; private set; }

    public string[] IncludeGenres { get; private set; } = [];

    public string[] ExcludeGenres { get; private set; } = [];

    public int? MaxRuntimeMinutes { get; private set; }

    public int? MinReleaseYear { get; private set; }

    public int? MaxReleaseYear { get; private set; }

    public string[] Keywords { get; private set; } = [];

    public bool TextIgnored { get; private set; }

    /// <summary>The text couldn't be read (AI failure or budget), so only the chips were used; implies <see cref="TextIgnored"/>.</summary>
    public bool TextUnavailable { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The room member whose mood step this is; null for a solo deck's mood. Deleted with the member.</summary>
    public Guid? RoomMemberId { get; private set; }

    /// <summary>The input as given, normalised: moods distinct, language lowercase, blank text null.</summary>
    public static MoodQuery Create(Guid? userId, IEnumerable<Mood> moods, RuntimeLimit maxRuntime, string? language, string? text, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(moods);
        if (userId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "userId must be a user or null.");
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
        IEnumerable<string> keywords, bool textIgnored, bool textUnavailable = false)
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
        TextIgnored = textIgnored || textUnavailable;
        TextUnavailable = textUnavailable;
    }

    /// <summary>
    /// Makes this a room member's mood step answer, after interpretation: it belongs to the member (and goes with them) and
    /// drops the text, since only the derived filters are ever used again (owner, issue #51).
    /// </summary>
    public void BelongToRoomMember(Guid roomMemberId)
    {
        if (roomMemberId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "roomMemberId is required.");
        }

        RoomMemberId = roomMemberId;
        Text = null;
    }

    private int? PlausibleYear(int? year) => year is { } y && y >= EarliestYear && y <= CreatedAt.Year + 1 ? y : null;
}
