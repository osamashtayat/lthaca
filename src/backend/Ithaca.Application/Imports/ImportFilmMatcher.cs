using Ithaca.Application.Catalog;
using Ithaca.Application.Common;
using Ithaca.Application.Tmdb;
using Ithaca.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ithaca.Application.Imports;

/// <summary>Where one import item ended up: a catalog film, or not found and why.</summary>
public sealed record ImportMatch(ImportItem Item, Guid? FilmId, bool AddedToCatalog, ImportNotFoundReason? NotFound);

/// <summary>
/// Matches a batch of import items to catalog films (api-contract.md §13 job rule 2): a Letterboxd URI matched before (by
/// any user) reuses its TMDB id; else our catalog by normalised title and exact year; else TMDB <c>search/movie</c> with
/// the year rule. A TMDB film we lack is added through <see cref="CatalogFilmWriter"/>, like the sync. TMDB calls are
/// paced and retried with back-off; a lookup still failing after <see cref="ImportOptions.TmdbAttempts"/> is
/// <see cref="ImportNotFoundReason.LookupFailed"/>. New URI matches are added as <see cref="LetterboxdFilmLink"/>s. Adds
/// to the context; <see cref="ImportService"/> saves. Collaborator of <see cref="ImportService"/> only.
/// </summary>
public sealed class ImportFilmMatcher(
    IAppDbContext db, ITmdbClient tmdb, CatalogFilmWriter catalog, IOptions<ImportOptions> options, TimeProvider time)
{
    public async Task<IReadOnlyList<ImportMatch>> MatchAsync(IReadOnlyList<ImportItem> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);
        var uris = items.Where(i => i.LetterboxdUri is not null).Select(i => i.LetterboxdUri!).ToList();
        var links = await db.LetterboxdFilmLinks.AsNoTracking()
            .Where(l => uris.Contains(l.LetterboxdUri))
            .ToDictionaryAsync(l => l.LetterboxdUri, l => l.TmdbId, StringComparer.Ordinal, ct);
        var years = items.Where(i => i.Year is not null).Select(i => i.Year).Distinct().ToList();
        var catalogByTitle = (await db.Films.AsNoTracking()
                .Where(f => years.Contains(f.ReleaseYear))
                .Select(f => new { f.TmdbId, f.Title, f.ReleaseYear, f.Popularity })
                .ToListAsync(ct))
            .GroupBy(f => (TitleNormalizer.Normalize(f.Title), f.ReleaseYear))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.Popularity ?? 0).ThenBy(f => f.TmdbId).First().TmdbId);

        // TMDB id per item, or why there is none.
        var found = new List<(ImportItem Item, int? TmdbId, ImportNotFoundReason? NotFound)>(items.Count);
        foreach (var item in items)
        {
            if (item.LetterboxdUri is { } uri && links.TryGetValue(uri, out var linked))
            {
                found.Add((item, linked, null));
            }
            else if (item.Year is { } year && catalogByTitle.TryGetValue((TitleNormalizer.Normalize(item.Title), year), out var local))
            {
                found.Add((item, local, null));
            }
            else
            {
                var searched = await SearchAsync(item.Title, item.Year, ct);
                found.Add(searched.IsSuccess
                    ? (item, searched.Value, searched.Value is null ? ImportNotFoundReason.NoMatch : null)
                    : (item, null, ImportNotFoundReason.LookupFailed));
            }
        }

        var tmdbIds = found.Where(f => f.TmdbId is not null).Select(f => f.TmdbId!.Value).Distinct().ToList();
        var films = await db.Films.AsNoTracking()
            .Where(f => tmdbIds.Contains(f.TmdbId))
            .Select(f => new { f.TmdbId, f.Id })
            .ToDictionaryAsync(f => f.TmdbId, f => f.Id, ct);
        var addedNow = new HashSet<int>();
        var matches = new List<ImportMatch>(items.Count);
        foreach (var (item, tmdbId, notFound) in found)
        {
            if (tmdbId is not { } id)
            {
                matches.Add(new ImportMatch(item, null, false, notFound));
                continue;
            }

            var added = false;
            if (!films.TryGetValue(id, out var filmId))
            {
                var details = await CallTmdbAsync(() => tmdb.GetFilmDetailsAsync(id, ct), ct);
                if (!details.IsSuccess)
                {
                    matches.Add(new ImportMatch(item, null, false, ImportNotFoundReason.LookupFailed));
                    continue;
                }

                if (details.Value is not { } d || catalog.Write(null, d, time.GetUtcNow()) is not { } written)
                {
                    matches.Add(new ImportMatch(item, null, false, ImportNotFoundReason.NoMatch));
                    continue;
                }

                filmId = written.Film.Id;
                films[id] = filmId;
                added = addedNow.Add(id);
            }

            if (item.LetterboxdUri is { } uri && !links.ContainsKey(uri))
            {
                db.LetterboxdFilmLinks.Add(LetterboxdFilmLink.Create(uri, id, time.GetUtcNow()));
                links[uri] = id;
            }

            matches.Add(new ImportMatch(item, filmId, added, null));
        }

        return matches;
    }

    /// <summary>
    /// The TMDB part of job rule 2. With a year: a result whose normalised title or original title equals the row's and
    /// whose release year equals it, else is one off; several → the most popular. The year-filtered search goes first; a
    /// second search without the filter catches films whose release TMDB files under the neighbouring year. Without a
    /// year: a match only when exactly one result has the same normalised title.
    /// </summary>
    private async Task<Result<int?>> SearchAsync(string title, int? year, CancellationToken ct)
    {
        var wanted = TitleNormalizer.Normalize(title);
        if (year is not { } y)
        {
            var all = await CallTmdbAsync(() => tmdb.SearchFilmsAsync(title, null, ct), ct);
            if (!all.IsSuccess)
            {
                return all.Error;
            }

            var same = all.Value.Where(r => SameTitle(r, wanted)).Select(r => r.TmdbId).Distinct().ToList();
            return Result<int?>.Success(same.Count == 1 ? same[0] : null);
        }

        var inYear = await CallTmdbAsync(() => tmdb.SearchFilmsAsync(title, y, ct), ct);
        if (!inYear.IsSuccess)
        {
            return inYear.Error;
        }

        if (Best(inYear.Value, wanted, y) is { } best)
        {
            return best;
        }

        var any = await CallTmdbAsync(() => tmdb.SearchFilmsAsync(title, null, ct), ct);
        return any.IsSuccess ? Best(any.Value, wanted, y) : any.Error;
    }

    private static int? Best(IReadOnlyList<TmdbSearchResult> results, string wanted, int year)
    {
        var titled = results.Where(r => SameTitle(r, wanted)).ToList();
        var pick = titled.Where(r => r.ReleaseYear == year).ToList() is { Count: > 0 } exact
            ? exact
            : titled.Where(r => r.ReleaseYear is { } ry && Math.Abs(ry - year) == 1).ToList();
        return pick.Count == 0 ? null : pick.OrderByDescending(r => r.Popularity).ThenBy(r => r.TmdbId).First().TmdbId;
    }

    private static bool SameTitle(TmdbSearchResult result, string wanted) =>
        TitleNormalizer.Normalize(result.Title) == wanted
        || (result.OriginalTitle is { } original && TitleNormalizer.Normalize(original) == wanted);

    /// <summary>One TMDB call, paced by <see cref="ImportOptions.TmdbRequestDelay"/>, retried with doubling back-off.</summary>
    private async Task<Result<T>> CallTmdbAsync<T>(Func<Task<Result<T>>> call, CancellationToken ct)
    {
        var settings = options.Value;
        var backOff = settings.TmdbRetryDelay;
        for (var attempt = 1; ; attempt++)
        {
            if (settings.TmdbRequestDelay > TimeSpan.Zero)
            {
                await Task.Delay(settings.TmdbRequestDelay, time, ct);
            }

            var result = await call();
            if (result.IsSuccess || attempt >= settings.TmdbAttempts)
            {
                return result;
            }

            if (backOff > TimeSpan.Zero)
            {
                await Task.Delay(backOff, time, ct);
            }

            backOff *= 2;
        }
    }
}
