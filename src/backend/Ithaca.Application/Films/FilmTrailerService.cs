using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;
using Ithaca.Domain.Films;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ithaca.Application.Films;

/// <summary>
/// A film's trailer (api-contract.md §8): the first YouTube trailer TMDB lists, cached per film (<see cref="FilmTrailer"/>,
/// ADR-0003). A cached answer younger than <see cref="FilmTrailer.RefreshAfter"/> is served without calling TMDB; an older
/// one is refreshed, and served if TMDB fails while it is younger than <see cref="FilmTrailer.MaxAge"/> (ADR-0017).
/// <c>tmdb.unavailable</c> only when TMDB fails and nothing servable is cached. "No trailer" is cached too.
/// </summary>
public sealed partial class FilmTrailerService(IAppDbContext db, ITmdbClient tmdb, TimeProvider time, ILogger<FilmTrailerService> logger)
{
    private const string YouTube = "YouTube";

    public async Task<Result<FilmTrailerDto>> GetAsync(int tmdbId, CancellationToken ct)
    {
        var film = await db.Films
            .AsNoTracking()
            .Where(f => f.TmdbId == tmdbId)
            .Select(f => new { f.Id, f.IsAvailable })
            .SingleOrDefaultAsync(ct);
        if (film is null)
        {
            return FilmErrors.NotFound;
        }

        // TMDB no longer knows the film (ADR-0019, A14): it has no videos either.
        if (!film.IsAvailable)
        {
            return new FilmTrailerDto(null);
        }

        var now = time.GetUtcNow();
        var cached = await db.FilmTrailers.SingleOrDefaultAsync(t => t.FilmId == film.Id, ct);
        if (cached is not null && cached.IsFresh(now))
        {
            return ToDto(cached);
        }

        var videos = await tmdb.GetTrailersAsync(tmdbId, ct);
        if (!videos.IsSuccess)
        {
            return cached is not null && cached.IsServable(now) ? ToDto(cached) : videos.Error;
        }

        var first = videos.Value.FirstOrDefault(v =>
            string.Equals(v.Site, YouTube, StringComparison.OrdinalIgnoreCase) && FilmTrailer.IsValidKey(v.Key));
        var inserted = cached is null;
        if (cached is null)
        {
            cached = FilmTrailer.Create(film.Id, first?.Key, first?.Name, now);
            db.FilmTrailers.Add(cached);
        }
        else
        {
            cached.Set(first?.Key, first?.Name, now);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception e) when (inserted && WriteConflicts.IsWriteConflict(e))
        {
            // Two first requests for the same film raced and the other one cached it: the answer in hand is just as good.
            LogInsertRace(logger, tmdbId);
        }

        return ToDto(cached);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Trailer cache insert for TMDB id {TmdbId} lost a race; served the fetched answer")]
    private static partial void LogInsertRace(ILogger logger, int tmdbId);

    private static FilmTrailerDto ToDto(FilmTrailer trailer) =>
        new(trailer.Key is { } key ? new TrailerDto(TrailerSite.YouTube, key, trailer.Name ?? "") : null);
}
