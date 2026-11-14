using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Library;

/// <summary>
/// A user's relation to one film (glossary: <c>UserFilm</c>): independent Liked / Watchlist / Seen flags and a 0.5–5 rating
/// in steps of 0.5 (ADR-0024), stored as a count of half stars (1–10). This class is the only place that converts.
/// A rating implies seen (A8). There is no "watching" state (A9). A row with nothing set has no reason to exist: the
/// service deletes it (<see cref="IsEmpty"/>). Its film can't be purged while it exists (ADR-0019).
/// </summary>
public sealed class UserFilm
{
    /// <summary>Lowest stored value: one half star (rating 0.5).</summary>
    public const short MinHalfStars = 1;

    /// <summary>Highest stored value: ten half stars (rating 5).</summary>
    public const short MaxHalfStars = 10;

    /// <summary>The top of the rating scale (five stars).</summary>
    public const decimal MaxRating = MaxHalfStars / 2m;

    /// <summary>The half-step rule in words, for validation messages.</summary>
    public const string RatingRule = "rating must be 0.5-5 in steps of 0.5.";

    private UserFilm(Guid userId, Guid filmId, DateTimeOffset now)
    {
        UserId = userId;
        FilmId = filmId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>For persistence materialization only.</summary>
    private UserFilm()
    {
    }

    public Guid UserId { get; private set; }

    public Guid FilmId { get; private set; }

    public bool Liked { get; private set; }

    public bool OnWatchlist { get; private set; }

    public bool Seen { get; private set; }

    /// <summary>The persisted rating: <see cref="MinHalfStars"/>–<see cref="MaxHalfStars"/> half stars, or null. Sort on this.</summary>
    public short? RatingHalfStars { get; private set; }

    /// <summary>The rating the API shows: 0.5–5 in steps of 0.5, or null.</summary>
    public decimal? Rating => ToRating(RatingHalfStars);

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsEmpty => !Liked && !OnWatchlist && !Seen && RatingHalfStars is null;

    /// <summary>The half-step rule (ADR-0024): 0.5, 1, 1.5 … 5. Used by request validation and by <see cref="Set"/>.</summary>
    public static bool IsValidRating(decimal rating)
    {
        var halfStars = rating * 2;
        return halfStars == decimal.Truncate(halfStars) && halfStars is >= MinHalfStars and <= MaxHalfStars;
    }

    /// <summary>Stored half stars → API rating. For query projections that read <see cref="RatingHalfStars"/>.</summary>
    public static decimal? ToRating(short? halfStars) => halfStars is { } h ? h / 2m : null;

    public static UserFilm Create(Guid userId, Guid filmId, DateTimeOffset now)
    {
        if (userId == Guid.Empty || filmId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "userId and filmId are required.");
        }

        return new UserFilm(userId, filmId, now);
    }

    /// <summary>Replaces the whole state (last write wins). A rating forces <see cref="Seen"/>.</summary>
    public void Set(bool liked, bool onWatchlist, bool seen, decimal? rating, DateTimeOffset now)
    {
        if (rating is { } r && !IsValidRating(r))
        {
            throw new DomainException(ErrorCodes.Validation, RatingRule, "rating");
        }

        Liked = liked;
        OnWatchlist = onWatchlist;
        Seen = seen || rating is not null;
        RatingHalfStars = rating is { } value ? (short)(value * 2) : null;
        UpdatedAt = now;
    }

    /// <summary>Changes only the watchlist flag. Returns false (and leaves <see cref="UpdatedAt"/> alone) when it already had that value.</summary>
    public bool SetWatchlist(bool onWatchlist, DateTimeOffset now)
    {
        if (OnWatchlist == onWatchlist)
        {
            return false;
        }

        OnWatchlist = onWatchlist;
        UpdatedAt = now;
        return true;
    }

    /// <summary>
    /// An "up" swipe (seen it): sets <see cref="Seen"/>, and <see cref="Liked"/> too when they loved it. Only ever turns flags
    /// on; the rating and watchlist are left alone. <see cref="UpdatedAt"/> moves only when something changed.
    /// </summary>
    public void MarkSeen(bool loved, DateTimeOffset now)
    {
        if (Seen && (Liked || !loved))
        {
            return;
        }

        Seen = true;
        Liked = Liked || loved;
        UpdatedAt = now;
    }
}
