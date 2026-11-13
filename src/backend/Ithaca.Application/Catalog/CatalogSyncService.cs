using Ithaca.Application.Ai;
using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;
using Ithaca.Domain.Films;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ithaca.Application.Catalog;

/// <summary>
/// TMDB → <c>films</c> sync (ADR-0003). Pages through TMDB's most popular films, upserts each by <c>TmdbId</c>, optionally
/// embeds it (off by default, ADR-0017), then deletes films not refreshed within <see cref="CatalogSyncOptions.MaxAgeDays"/>
/// (TMDB's 6-month caching limit), then gives the remaining films their Wikidata facts
/// (<see cref="WikidataEnrichmentService"/>). Saves once per page (and once per Wikidata batch), so a failure mid-run keeps
/// the work already done; the next run continues. Called by <c>CatalogSyncJob</c> and the <c>--sync-catalog</c> command.
/// </summary>
public sealed class CatalogSyncService(
    IAppDbContext db,
    ITmdbClient tmdb,
    IEmbeddingClient embeddings,
    WikidataEnrichmentService facts,
    IOptions<CatalogSyncOptions> options,
    TimeProvider time)
{
    public async Task<Result<CatalogSyncReport>> RunAsync(CancellationToken ct)
    {
        var settings = options.Value;
        int pages = 0, added = 0, updated = 0, skipped = 0, embedded = 0;

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
                var textChanged = film.UpdateDetails(d.Title, d.ReleaseYear, d.Overview, d.PosterPath, d.RuntimeMinutes, [.. d.Genres], d.Popularity, now);
                if (isNew)
                {
                    db.Films.Add(film);
                    added++;
                }
                else
                {
                    updated++;
                }

                if (settings.EmbedOverviews && (textChanged || film.EmbeddingModel != embeddings.ModelName))
                {
                    film.SetEmbedding(await embeddings.EmbedAsync(EmbeddingText(film), ct), embeddings.ModelName);
                    embedded++;
                }
            }

            await db.SaveChangesAsync(ct);
            pages++;
        }

        var cutoff = time.GetUtcNow().AddDays(-settings.MaxAgeDays);
        var purged = await db.Films.Where(f => f.SyncedAt < cutoff).ExecuteDeleteAsync(ct);

        // After the purge, so no batch is spent on films about to go. A Wikidata failure fails the run; TMDB work is kept.
        var enrichment = await facts.RunAsync(ct);
        if (!enrichment.IsSuccess)
        {
            return enrichment.Error;
        }

        return new CatalogSyncReport(pages, added, updated, skipped, embedded, purged, enrichment.Value.Enriched, enrichment.Value.Unmatched);
    }

    /// <summary>What a film's vector represents. Changing this means re-embedding the catalog.</summary>
    private static string EmbeddingText(Film film) => $"{film.Title}\n{string.Join(", ", film.Genres)}\n{film.Overview}";
}
