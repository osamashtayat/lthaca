using Ithaca.Application.Auth;
using Ithaca.Application.Common;
using Ithaca.Domain.Moods;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Films;

/// <summary>
/// Reference slice (copy this shape): a query service projects straight to the DTO with <c>AsNoTracking</c> + <c>Select</c>
/// and returns <see cref="Result{T}"/>. No entity leaves the service; no mapping happens in the controller.
/// </summary>
public sealed class FilmQueryService(IAppDbContext db, ICurrentActor current)
{

    public async Task<Result<FilmDto>> GetByTmdbIdAsync(int tmdbId, CancellationToken ct)
    {
        var film = await db.Films
            .AsNoTracking()
            .Where(f => f.TmdbId == tmdbId)
            .Select(FilmProjections.Dto)
            .SingleOrDefaultAsync(ct);

        return film is null ? FilmErrors.NotFound : film;
    }

    /// <summary>
    /// Browse the catalog (api-contract.md §9): every filter that is set narrows the result. Films TMDB no longer knows are
    /// left out. <c>hideSeen</c> only applies to a signed-in user. Every order ends on TMDB id so pages are stable.
    /// </summary>
    public async Task<Result<PagedResult<FilmSummaryDto>>> BrowseAsync(FilmBrowseRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var films = db.Films.AsNoTracking().Where(f => f.IsAvailable);

        if (request.Search?.Trim() is { Length: > 0 } search)
        {
            // EF translates this to lower(title) LIKE '%needle%' (escaping % and _). Only ToLower() has a SQL translation; the
            // StringComparison overload and ToLowerInvariant() have none.
#pragma warning disable CA1304, CA1311, CA1862 // SQL lower() is the intended, culture-free translation.
            var needle = search.ToLower();
            films = films.Where(f => f.Title.ToLower().Contains(needle));
#pragma warning restore CA1304, CA1311, CA1862
        }

        if (request.Genre?.Trim() is { Length: > 0 } genre)
        {
            films = films.Where(f => f.Genres.Contains(genre));
        }

        if (request.MaxRuntime?.MaxMinutes() is { } maxMinutes)
        {
            films = films.Where(f => f.RuntimeMinutes <= maxMinutes);
        }

        if (request.MinVoteAverage is { } minVote)
        {
            films = films.Where(f => f.VoteAverage >= minVote);
        }

        if (request.HideSeen && current.Actor is { IsUser: true } actor)
        {
            var userId = actor.Id;
            films = films.Where(f => !db.UserFilms.Any(u => u.UserId == userId && u.FilmId == f.Id && u.Seen));
        }

        var ordered = request.Sort switch
        {
            FilmSort.Newest => films.OrderBy(f => f.ReleaseYear == null).ThenByDescending(f => f.ReleaseYear).ThenBy(f => f.TmdbId),
            FilmSort.Rating => films.OrderBy(f => f.VoteAverage == null).ThenByDescending(f => f.VoteAverage).ThenBy(f => f.TmdbId),
            _ => films.OrderBy(f => f.Popularity == null).ThenByDescending(f => f.Popularity).ThenBy(f => f.TmdbId),
        };

        return await ordered
            .Select(f => new FilmSummaryDto(f.TmdbId, f.Title, f.ReleaseYear, f.PosterPath, f.RuntimeMinutes, f.Genres, f.VoteAverage))
            .ToPagedResultAsync(request, ct);
    }

    /// <summary>Genres present in our catalog (available films), alphabetical.</summary>
    public async Task<Result<IReadOnlyList<string>>> GetGenresAsync(CancellationToken ct)
    {
        IReadOnlyList<string> genres = await CatalogGenres.ListAsync(db, ct);
        return Result<IReadOnlyList<string>>.Success(genres);
    }
}
