using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;
using Ithaca.Domain.Films;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ithaca.Application.Catalog;

/// <summary>
/// TMDB → <c>films</c> sync (ADR-0003). Pages through TMDB's most popular films and upserts each by <c>TmdbId</c> (display
/// data only); re-fetches by id every film user data references that this run hasn't refreshed yet, marking the ones TMDB
/// no longer knows unavailable (ADR-0019); deletes only unreferenced films not refreshed within
/// <see cref="CatalogSyncOptions.MaxAgeDays"/> (TMDB's 6-month caching limit); gives the remaining films their Wikidata
/// facts (<see cref="WikidataEnricher"/>); then embeds the facts' text (<see cref="FilmEmbedder"/>; never TMDB text,
/// ADR-0017). Saves once per page and once per refresh, Wikidata or embedding batch, so a failure mid-run keeps the work
/// already done; the next run continues. A failure before the purge skips it. Called by <c>CatalogSyncJob</c> and the
/// <c>--sync-catalog</c> command.
/// </summary>
public sealed class CatalogSyncService(
    IAppDbContext db,
    ITmdbClient tmdb,
    WikidataEnricher facts,
    FilmEmbedder embedder,
    IOptions<CatalogSyncOptions> options,
    TimeProvider time)
{
    /// <summary>Referenced films per save in the refresh step.</summary>
    private const int RefreshBatchSize = 50;

    public async Task<Result<CatalogSyncReport>> RunAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var runStartedAt = time.GetUtcNow();
        int pages = 0, added = 0, updated = 0, skipped = 0;

        for (var page = 1; page <= settings.Pages; page++)
        {
            var summaries = await tmdb.GetCatalogPageAsync(page, ct);
            if (!summaries.IsSuccess)
            {
                return summaries.Error;
            }

            if (summaries.Value.Count == 0)
            {
                break;
            }

            var ids = summaries.Value.Select(s => s.TmdbId).ToList();
            var existing = await db.Films.Where(f => ids.Contains(f.TmdbId)).ToDictionaryAsync(f => f.TmdbId, ct);

            foreach (var summary in summaries.Value)
            {
                var details = await tmdb.GetFilmDetailsAsync(summary.TmdbId, ct);
                if (!details.IsSuccess)
                {
                    return details.Error;
                }

                if (details.Value is not { } d || !Film.IsValidTitle(d.Title))
                {
                    skipped++;
                    continue;
                }

                var now = time.GetUtcNow();
                var isNew = !existing.TryGetValue(d.TmdbId, out var film);
                film ??= Film.Create(d.TmdbId, d.Title, now);
                film.UpdateDetails(d.Title, d.ReleaseYear, d.Overview, d.PosterPath, d.RuntimeMinutes, [.. d.Genres], d.Popularity, d.BackdropPath, d.VoteAverage, d.VoteCount, d.OriginalLanguage, now);
                if (isNew)
                {
                    db.Films.Add(film);
                    added++;
                }
                else
                {
                    updated++;
                }
            }

            await db.SaveChangesAsync(ct);
            pages++;
        }

        var refresh = await RefreshReferencedAsync(runStartedAt, ct);
        if (!refresh.IsSuccess)
        {
            return refresh.Error;
        }

        // Orphans only: referenced films were just refreshed, and the RESTRICT foreign keys would reject them anyway.
        var cutoff = time.GetUtcNow().AddDays(-settings.MaxAgeDays);
        var referenced = ReferencedFilmIds();
        var purged = await db.Films.Where(f => f.SyncedAt < cutoff && !referenced.Contains(f.Id)).ExecuteDeleteAsync(ct);

        // After the purge, so no batch is spent on films about to go. A Wikidata failure fails the run; TMDB work is kept.
        var enrichment = await facts.RunAsync(ct);
        if (!enrichment.IsSuccess)
        {
            return enrichment.Error;
        }

        var embedded = await embedder.RunAsync(ct);
        if (!embedded.IsSuccess)
        {
            return embedded.Error;
        }

        return new CatalogSyncReport(
            pages, added, updated, skipped, embedded.Value, purged, enrichment.Value.Enriched, enrichment.Value.Unmatched,
            refresh.Value.Refreshed, refresh.Value.Unavailable);
    }

    /// <summary>
    /// The films user data points at: the one definition of "referenced" for the refresh and the purge (ADR-0019). Tasks
    /// that add references to films (deck items, votes) extend it.
    /// </summary>
    private IQueryable<Guid> ReferencedFilmIds() => db.UserFilms.Select(u => u.FilmId);

    /// <summary>
    /// Re-fetches by TMDB id every referenced film this run hasn't refreshed (it may have left TMDB's popular pages), in
    /// TMDB-id order, saving every <see cref="RefreshBatchSize"/>. A 404 (or a reply without a valid title) keeps the row
    /// but clears its TMDB display fields.
    /// </summary>
    private async Task<Result<(int Refreshed, int Unavailable)>> RefreshReferencedAsync(DateTimeOffset runStartedAt, CancellationToken ct)
    {
        var referenced = ReferencedFilmIds();
        int refreshed = 0, unavailable = 0, lastTmdbId = 0;
        while (true)
        {
            var afterTmdbId = lastTmdbId;
            var films = await db.Films
                .Where(f => f.TmdbId > afterTmdbId && f.SyncedAt < runStartedAt && referenced.Contains(f.Id))
                .OrderBy(f => f.TmdbId)
                .Take(RefreshBatchSize)
                .ToListAsync(ct);
            if (films.Count == 0)
            {
                return (refreshed, unavailable);
            }

            foreach (var film in films)
            {
                var details = await tmdb.GetFilmDetailsAsync(film.TmdbId, ct);
                if (!details.IsSuccess)
                {
                    return details.Error;
                }

                // Gone from TMDB, or served without a usable title: either way we can't refresh it, and keeping its old TMDB
                // data past MaxAgeDays is what ADR-0019 rules out.
                var now = time.GetUtcNow();
                if (details.Value is { } d && Film.IsValidTitle(d.Title))
                {
                    film.UpdateDetails(d.Title, d.ReleaseYear, d.Overview, d.PosterPath, d.RuntimeMinutes, [.. d.Genres], d.Popularity, d.BackdropPath, d.VoteAverage, d.VoteCount, d.OriginalLanguage, now);
                    refreshed++;
                }
                else
                {
                    film.MarkUnavailable(now);
                    unavailable++;
                }
            }

            await db.SaveChangesAsync(ct);
            lastTmdbId = films[^1].TmdbId;
        }
    }
}
