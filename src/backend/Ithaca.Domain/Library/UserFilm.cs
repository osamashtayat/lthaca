using Ithaca.Domain.Errors;

namespace Ithaca.Domain.Library;

/// <summary>
/// A user's relation to one film (glossary: <c>UserFilm</c>): independent Liked / Watchlist / Seen flags and a 1–5 rating.
/// A rating implies seen (A8). There is no "watching" state (A9). A row with nothing set has no reason to exist: the
/// service deletes it (<see cref="IsEmpty"/>). Its film can't be purged while it exists (ADR-0019).
/// </summary>
public sealed class UserFilm
{
    public const int MinRating = 1;
    public const int MaxRating = 5;

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

    /// <summary><see cref="MinRating"/>–<see cref="MaxRating"/> ticket stubs, or null.</summary>
    public int? Rating { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsEmpty => !Liked && !OnWatchlist && !Seen && Rating is null;

    public static UserFilm Create(Guid userId, Guid filmId, DateTimeOffset now)
    {
        if (userId == Guid.Empty || filmId == Guid.Empty)
        {
            throw new DomainException(ErrorCodes.Validation, "userId and filmId are required.");
        }

        return new UserFilm(userId, filmId, now);
    }

    /// <summary>Replaces the whole state (last write wins). A rating forces <see cref="Seen"/>.</summary>
    public void Set(bool liked, bool onWatchlist, bool seen, int? rating, DateTimeOffset now)
    {
        if (rating is < MinRating or > MaxRating)
        {
            throw new DomainException(ErrorCodes.Validation, $"rating must be {MinRating}-{MaxRating}.", "rating");
        }

        Liked = liked;
        OnWatchlist = onWatchlist;
        Seen = seen || rating is not null;
        Rating = rating;
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
}
