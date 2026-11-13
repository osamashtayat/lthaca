using Ithaca.Application.Common;
using Ithaca.Application.Wikidata;
using Ithaca.Domain.Films;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ithaca.Application.Catalog;

/// <summary>
/// Gives films their AI-safe <see cref="FilmFacts"/> from Wikidata (ADR-0017). Picks every film without facts, or with facts
/// older than <see cref="WikidataOptions.RefreshDays"/>, in TMDB-id order, <see cref="WikidataOptions.BatchSize"/> at a
/// time: one SPARQL query and one save per batch, so a failure keeps the batches already done and the next run picks up
/// the rest. Films Wikidata doesn't link are logged and skipped (and retried next run, as Wikidata grows).
/// A collaborator of <see cref="CatalogSyncService"/>, its only caller, not a use-case service of its own
/// (docs/backend-conventions.md: a service never calls another use-case service).
/// </summary>
public sealed partial class WikidataEnricher(
    IAppDbContext db,
    IWikidataClient wikidata,
    IOptions<WikidataOptions> options,
    TimeProvider time,
    ILogger<WikidataEnricher> logger)
{
    public async Task<Result<WikidataEnrichmentReport>> RunAsync(CancellationToken ct)
    {
        var settings = options.Value;
        var freshSince = time.GetUtcNow().AddDays(-settings.RefreshDays);
        int batches = 0, enriched = 0, unmatched = 0, lastTmdbId = 0;

        while (true)
        {
            var afterTmdbId = lastTmdbId;
            var films = await db.Films.AsNoTracking()
                .Where(f => f.TmdbId > afterTmdbId && !db.FilmFacts.Any(x => x.FilmId == f.Id && x.FetchedAt >= freshSince))
                .OrderBy(f => f.TmdbId)
                .Select(f => new { f.Id, f.TmdbId })
                .Take(settings.BatchSize)
                .ToListAsync(ct);
            if (films.Count == 0)
            {
                break;
            }

            var found = await wikidata.GetFilmsAsync([.. films.Select(f => f.TmdbId)], ct);
            if (!found.IsSuccess)
            {
                return found.Error;
            }

            var factsByTmdbId = found.Value.ToDictionary(w => w.TmdbId, w => w.Facts);
            var filmIds = films.Select(f => f.Id).ToList();
            var existing = await db.FilmFacts.Where(x => filmIds.Contains(x.FilmId)).ToDictionaryAsync(x => x.FilmId, ct);
            var now = time.GetUtcNow();
            var missing = new List<int>();

            foreach (var film in films)
            {
                if (!factsByTmdbId.TryGetValue(film.TmdbId, out var facts))
                {
                    missing.Add(film.TmdbId);
                    continue;
                }

                if (existing.TryGetValue(film.Id, out var stored))
                {
                    stored.Update(facts, now);
                }
                else
                {
                    db.FilmFacts.Add(FilmFacts.Create(film.Id, facts, now));
                }

                enriched++;
            }

            if (missing.Count > 0)
            {
                unmatched += missing.Count;
                LogUnmatched(logger, missing.Count, missing);
            }

            await db.SaveChangesAsync(ct);
            batches++;
            lastTmdbId = films[^1].TmdbId;
        }

        return new WikidataEnrichmentReport(batches, enriched, unmatched);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Wikidata links no item to {Count} films, skipped: TMDB ids {TmdbIds}")]
    private static partial void LogUnmatched(ILogger logger, int count, IReadOnlyList<int> tmdbIds);
}

/// <summary>What one enrichment pass did: part of <see cref="CatalogSyncReport"/>.</summary>
public sealed record WikidataEnrichmentReport(int Batches, int Enriched, int Unmatched);
