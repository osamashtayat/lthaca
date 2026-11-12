using Ithaca.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Films;

/// <summary>
/// Reference slice (copy this shape): a query service projects straight to the DTO with <c>AsNoTracking</c> + <c>Select</c>
/// and returns <see cref="Result{T}"/>. No entity leaves the service; no mapping happens in the controller.
/// </summary>
public sealed class FilmQueryService(IAppDbContext db)
{
    public async Task<Result<FilmDto>> GetByTmdbIdAsync(int tmdbId, CancellationToken ct)
    {
        var film = await db.Films
            .AsNoTracking()
            .Where(f => f.TmdbId == tmdbId)
            .Select(f => new FilmDto(f.TmdbId, f.Title, f.ReleaseYear, f.Overview, f.PosterPath, f.RuntimeMinutes, f.Genres))
            .SingleOrDefaultAsync(ct);

        return film is null ? FilmErrors.NotFound : film;
    }
}
