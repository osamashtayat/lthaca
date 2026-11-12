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
            .Select(f => new FilmDto(f.TmdbId, f.Title, f.ReleaseYear, f.Overview, f.PosterPath, f.RuntimeMinutes, f.Genres, null, null, null, null))
            .SingleOrDefaultAsync(ct);

        return film is null ? FilmErrors.NotFound : film;
    }

    /// <summary>Browse the catalog (api-contract.md §9). Contract stub until BE-06.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = ContractStub.Justification)]
    public Task<Result<PagedResult<FilmSummaryDto>>> BrowseAsync(FilmBrowseRequest request, CancellationToken ct) =>
        ContractStub.Of<PagedResult<FilmSummaryDto>>();

    /// <summary>Genres present in our catalog, alphabetical. Contract stub until BE-06.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = ContractStub.Justification)]
    public Task<Result<IReadOnlyList<string>>> GetGenresAsync(CancellationToken ct) => ContractStub.Of<IReadOnlyList<string>>();
}
