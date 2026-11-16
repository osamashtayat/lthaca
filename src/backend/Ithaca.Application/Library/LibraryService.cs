using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Ithaca.Application.Films;
using Ithaca.Domain.Library;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Library;

/// <summary>
/// My Cinema: one <c>UserFilm</c> per user and film (api-contract.md §7, ADR-0019). Every method acts for the signed-in
/// user from <see cref="ICurrentActor"/>; guests and anonymous callers get <c>auth.unauthenticated</c> (the controller's
/// policy already stops them). Rules: a rating implies seen; a row with nothing set is deleted; the watchlist pair changes
/// only that flag and is idempotent.
/// </summary>
public sealed class LibraryService(IAppDbContext db, ICurrentActor current, TimeProvider time)
{
    public async Task<Result<PagedResult<UserFilmDto>>> ListAsync(LibraryListRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        var rows = db.UserFilms.AsNoTracking().Where(u => u.UserId == userId);
        rows = request.Shelf switch
        {
            LibraryShelf.Watchlist => rows.Where(u => u.OnWatchlist),
            LibraryShelf.Seen => rows.Where(u => u.Seen),
            _ => rows.Where(u => u.Liked),
        };

        var withFilms = rows.Join(db.Films, u => u.FilmId, f => f.Id, (u, f) => new { UserFilm = u, Film = f });
        if (!string.IsNullOrWhiteSpace(request.Genre))
        {
            var genre = request.Genre.Trim();
            withFilms = withFilms.Where(r => r.Film.Genres.Contains(genre));
        }

        // Every order ends on TMDB id so pages are stable.
        withFilms = request.Sort switch
        {
            LibrarySort.Title => withFilms.OrderBy(r => r.Film.Title).ThenBy(r => r.Film.TmdbId),
            LibrarySort.Rating => withFilms
                .OrderBy(r => r.UserFilm.RatingHalfStars == null)
                .ThenByDescending(r => r.UserFilm.RatingHalfStars)
                .ThenByDescending(r => r.UserFilm.UpdatedAt)
                .ThenBy(r => r.Film.TmdbId),
            _ => withFilms.OrderByDescending(r => r.UserFilm.UpdatedAt).ThenBy(r => r.Film.TmdbId),
        };

        return await withFilms
            .Select(r => new UserFilmDto(
                r.UserFilm.Liked,
                r.UserFilm.OnWatchlist,
                r.UserFilm.Seen,
                UserFilm.ToRating(r.UserFilm.RatingHalfStars),
                new FilmSummaryDto(r.Film.TmdbId, r.Film.Title, r.Film.ReleaseYear, r.Film.PosterPath, r.Film.RuntimeMinutes, r.Film.Genres, r.Film.VoteAverage),
                r.UserFilm.UpdatedAt))
            .ToPagedResultAsync(request, ct);
    }

    public async Task<Result<LibrarySummaryDto>> GetSummaryAsync(CancellationToken ct)
    {
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        var counts = await db.UserFilms.AsNoTracking()
            .Where(u => u.UserId == userId)
            .GroupBy(_ => 1)
            .Select(g => new LibrarySummaryDto(g.Count(u => u.Liked), g.Count(u => u.OnWatchlist), g.Count(u => u.Seen)))
            .SingleOrDefaultAsync(ct);
        return counts ?? new LibrarySummaryDto(0, 0, 0);
    }

    /// <summary>The caller's state for a film in our catalog; all false/null (and <c>updatedAt</c> = now) when untouched.</summary>
    public async Task<Result<UserFilmDto>> GetAsync(int tmdbId, CancellationToken ct)
    {
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        var film = await db.Films.AsNoTracking().Where(f => f.TmdbId == tmdbId).Select(f => new { f.Id }).SingleOrDefaultAsync(ct);
        if (film is null)
        {
            return FilmErrors.NotFound;
        }

        var userFilm = await db.UserFilms.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == userId && u.FilmId == film.Id, ct);
        return await ToDtoAsync(film.Id, userFilm, ct);
    }

    /// <summary>Replaces the whole state (last write wins). All false + no rating deletes the row.</summary>
    public async Task<Result<UserFilmDto>> UpdateAsync(int tmdbId, UpdateUserFilmRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        var filmId = await FilmIdAsync(tmdbId, ct);
        if (filmId is null)
        {
            return FilmErrors.NotFound;
        }

        var now = time.GetUtcNow();
        var userFilm = await db.UserFilms.SingleOrDefaultAsync(u => u.UserId == userId && u.FilmId == filmId, ct);
        if (userFilm is null)
        {
            userFilm = UserFilm.Create(userId, filmId.Value, now);
            db.UserFilms.Add(userFilm);
        }

        userFilm.Set(request.Liked, request.OnWatchlist, request.Seen, request.Rating, now);
        if (userFilm.IsEmpty)
        {
            db.UserFilms.Remove(userFilm);
        }

        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(filmId.Value, userFilm.IsEmpty ? null : userFilm, ct);
    }

    /// <summary>Idempotent; other flags untouched (the deck card's watchlist button).</summary>
    public Task<Result> AddToWatchlistAsync(int tmdbId, CancellationToken ct) => SetWatchlistAsync(tmdbId, onWatchlist: true, ct);

    /// <summary>Idempotent; other flags untouched.</summary>
    public Task<Result> RemoveFromWatchlistAsync(int tmdbId, CancellationToken ct) => SetWatchlistAsync(tmdbId, onWatchlist: false, ct);

    private async Task<Result> SetWatchlistAsync(int tmdbId, bool onWatchlist, CancellationToken ct)
    {
        if (UserId() is not { } userId)
        {
            return AuthErrors.Unauthenticated;
        }

        var filmId = await FilmIdAsync(tmdbId, ct);
        if (filmId is null)
        {
            return FilmErrors.NotFound;
        }

        var now = time.GetUtcNow();
        var userFilm = await db.UserFilms.SingleOrDefaultAsync(u => u.UserId == userId && u.FilmId == filmId, ct);
        if (userFilm is null)
        {
            if (!onWatchlist)
            {
                return Result.Success(); // nothing to remove
            }

            userFilm = UserFilm.Create(userId, filmId.Value, now);
            userFilm.SetWatchlist(onWatchlist: true, now);
            db.UserFilms.Add(userFilm);
        }
        else if (!userFilm.SetWatchlist(onWatchlist, now))
        {
            return Result.Success(); // already in that state
        }
        else if (userFilm.IsEmpty)
        {
            db.UserFilms.Remove(userFilm);
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private Guid? UserId() => current.Actor is { IsUser: true } actor ? actor.Id : null;

    private async Task<Guid?> FilmIdAsync(int tmdbId, CancellationToken ct) =>
        await db.Films.Where(f => f.TmdbId == tmdbId).Select(f => (Guid?)f.Id).SingleOrDefaultAsync(ct);

    private async Task<UserFilmDto> ToDtoAsync(Guid filmId, UserFilm? userFilm, CancellationToken ct)
    {
        var film = await db.Films.AsNoTracking()
            .Where(f => f.Id == filmId)
            .Select(f => new FilmSummaryDto(f.TmdbId, f.Title, f.ReleaseYear, f.PosterPath, f.RuntimeMinutes, f.Genres, f.VoteAverage))
            .SingleAsync(ct);
        return userFilm is null
            ? new UserFilmDto(false, false, false, null, film, time.GetUtcNow())
            : new UserFilmDto(userFilm.Liked, userFilm.OnWatchlist, userFilm.Seen, userFilm.Rating, film, userFilm.UpdatedAt);
    }
}
