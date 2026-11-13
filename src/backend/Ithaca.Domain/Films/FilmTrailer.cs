using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Films;

/// <summary>
/// The cached answer to "what is this film's trailer?" (ADR-0003): the first YouTube trailer TMDB lists, or none
/// (<see cref="Key"/> null, so a film without a trailer doesn't ask TMDB on every visit). One row per film, deleted with it.
/// TMDB data, so it is never served or kept past <see cref="MaxAge"/> (ADR-0017); after <see cref="RefreshAfter"/> the
/// next request asks TMDB again (new trailers appear) and falls back to this row if TMDB fails.
/// </summary>
public sealed class FilmTrailer
{
    public const int KeyMaxLength = 100;
    public const int NameMaxLength = 300;

    /// <summary>Served without asking TMDB for this long.</summary>
    public static readonly TimeSpan RefreshAfter = TimeSpan.FromDays(30);

    /// <summary>TMDB's terms cap caching at 6 months (ADR-0017): never served past this, and the nightly sync deletes it.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(180);

    private FilmTrailer(Guid filmId)
    {
        FilmId = filmId;
    }

    /// <summary>For persistence materialization only.</summary>
    private FilmTrailer()
    {
    }

    public Guid FilmId { get; private set; }

    /// <summary>The YouTube video key, or null when the film has no YouTube trailer.</summary>
    public string? Key { get; private set; }

    /// <summary>The video's title on TMDB; null exactly when <see cref="Key"/> is.</summary>
    public string? Name { get; private set; }

    public DateTimeOffset FetchedAt { get; private set; }

    public static FilmTrailer Create(Guid filmId)
    {
        if (filmId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "filmId is required.");
        }

        return new FilmTrailer(filmId);
    }

    /// <summary>The one key rule: 1–<see cref="KeyMaxLength"/> characters, not blank.</summary>
    public static bool IsValidKey(string? key) => !string.IsNullOrWhiteSpace(key) && key.Length <= KeyMaxLength;

    /// <summary>Usable at all: younger than <see cref="MaxAge"/>.</summary>
    public bool IsServable(DateTimeOffset now) => now - FetchedAt < MaxAge;

    /// <summary>Served as is, without asking TMDB: younger than <see cref="RefreshAfter"/>.</summary>
    public bool IsFresh(DateTimeOffset now) => now - FetchedAt < RefreshAfter;

    /// <summary>Stores TMDB's latest answer: a trailer (key and name) or none (both null).</summary>
    public void Set(string? key, string? name, DateTimeOffset now)
    {
        if (key is not null && !IsValidKey(key))
        {
            throw new DomainException(ErrorCodes.Validation, $"key must be 1-{KeyMaxLength} characters.", "key");
        }

        var trimmedName = (name ?? "").Trim();
        Key = key;
        Name = key is null ? null : trimmedName[..Math.Min(trimmedName.Length, NameMaxLength)];
        FetchedAt = now;
    }
}
