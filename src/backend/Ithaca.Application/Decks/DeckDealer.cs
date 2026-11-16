using Ithaca.Application.Common;
using Ithaca.Domain.Decks;
using Ithaca.Domain.Films;
using Ithaca.Domain.Moods;
using Ithaca.Domain.Taste;
using Microsoft.EntityFrameworkCore;

namespace Ithaca.Application.Decks;

/// <summary>
/// Deals the next cards of a deck, solo or a room's (collaborator of <see cref="DeckService"/> and <c>RoomDeckService</c>,
/// not a use-case service). Candidates are the most popular available films of our own catalog (ADR-0003) that pass the
/// <see cref="DeckFilter"/> and aren't in the deck yet; the <see cref="DeckSampler"/> picks among them. Adds the items to the
/// context without saving: the caller saves once.
/// </summary>
public sealed class DeckDealer(IAppDbContext db)
{
    /// <summary>Most popular eligible films the sampler chooses from per batch.</summary>
    private const int CandidatePool = 500;

    /// <summary>
    /// Adds up to <paramref name="count"/> items at positions after <paramref name="dealt"/>. Tries <paramref name="filters"/>
    /// in order, each topping up what the previous one couldn't fill (a room falls back to the sampler).
    /// <paramref name="excludedFilms"/> are never dealt (a solo user's seen films). Vectors are used only from
    /// <paramref name="embeddingModel"/>. With a <paramref name="taste"/> (decks by taste, BE-14) the picks lean towards it;
    /// pass its model as <paramref name="embeddingModel"/>.
    /// </summary>
    public async Task<List<DeckItem>> DealAsync(
        Guid deckId,
        int dealt,
        int count,
        IReadOnlyList<DeckFilter> filters,
        IQueryable<Guid>? excludedFilms,
        IReadOnlyList<DeckSignal> signals,
        string? embeddingModel,
        TasteProfile? taste,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filters);
        var picked = new List<Guid>();
        foreach (var filter in filters)
        {
            if (picked.Count >= count)
            {
                break;
            }

            var inDeck = db.DeckItems.Where(i => i.DeckId == deckId).Select(i => i.FilmId);
            var films = Filtered(db.Films.AsNoTracking(), filter).Where(f => f.IsAvailable && !inDeck.Contains(f.Id) && !picked.Contains(f.Id));
            if (excludedFilms is not null)
            {
                films = films.Where(f => !excludedFilms.Contains(f.Id));
            }

            var candidates = await films
                .OrderBy(f => f.Popularity == null).ThenByDescending(f => f.Popularity).ThenBy(f => f.TmdbId)
                .Take(CandidatePool)
                .Select(f => new SamplerFilm(
                    f.Id, f.Genres, f.ReleaseYear, f.RuntimeMinutes, f.Popularity ?? 0,
                    embeddingModel != null && f.EmbeddingModel == embeddingModel ? f.Embedding : null))
                .ToListAsync(ct);
            picked.AddRange(DeckSampler.Pick(candidates, signals, count - picked.Count, Random.Shared, taste));
        }

        var items = picked.Select((filmId, i) => DeckItem.Create(deckId, filmId, dealt + i + 1, now)).ToList();
        db.DeckItems.AddRange(items);
        return items;
    }

    /// <summary>
    /// The filter as hard limits. A film missing the value a mood limit needs doesn't pass it; a hard no excludes a film with
    /// any of its TMDB genres, a Wikidata genre label containing one of its terms, or (subtitles) a known non-English language.
    /// </summary>
    private IQueryable<Film> Filtered(IQueryable<Film> films, DeckFilter filter)
    {
        if (filter.IncludeGenres is { Count: > 0 } include)
        {
            films = films.Where(f => f.Genres.Any(g => include.Contains(g)));
        }

        var excluded = filter.ExcludeGenres.Concat(filter.HardNos.TmdbGenres).Distinct(StringComparer.Ordinal).ToList();
        if (excluded.Count > 0)
        {
            films = films.Where(f => !f.Genres.Any(g => excluded.Contains(g)));
        }

        // Terms are lowercase; SQL lower() is the intended, culture-free translation (as in FilmQueryService).
#pragma warning disable CA1304, CA1311, CA1862
        foreach (var term in filter.HardNos.WikidataGenreTerms)
        {
            films = films.Where(f => !db.FilmFacts.Any(x => x.FilmId == f.Id && x.Genres.Any(g => g.ToLower().Contains(term))));
        }
#pragma warning restore CA1304, CA1311, CA1862

        if (filter.HardNos.NonEnglish)
        {
            films = films.Where(f => f.OriginalLanguage == null || f.OriginalLanguage == HardNoRules.NoSubtitlesLanguage);
        }

        if (filter.MaxRuntimeMinutes is { } minutes)
        {
            films = films.Where(f => f.RuntimeMinutes <= minutes);
        }

        if (filter.MinReleaseYear is { } from)
        {
            films = films.Where(f => f.ReleaseYear >= from);
        }

        if (filter.MaxReleaseYear is { } to)
        {
            films = films.Where(f => f.ReleaseYear <= to);
        }

        if (filter.Language is { } language)
        {
            films = films.Where(f => f.OriginalLanguage == language);
        }

        return films;
    }
}
